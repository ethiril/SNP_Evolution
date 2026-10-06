using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Verification;
using SnpEvolution.Export;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;
using static SnpEvolution.Cli.CommandOptions;

namespace SnpEvolution.Cli
{
    // Exits with 1 for a bad command or a network that cannot be exported, and 3 when the co-simulation differs.
    internal static class ExportCommands
    {
        internal const int Differs = 3;

        // Cases are a part's contract cases, or one run of --steps steps with no input for a network.
        private sealed record Source(string Name, Network Network, Part? Part, IReadOnlyList<NetworkPort> Ports,
            IReadOnlyList<(string Label, InputSpikes Input, int Steps)> Cases);

        internal static int Verilog(IReadOnlyDictionary<string, string> options)
        {
            if (Read(options) is not Source source)
            {
                return 1;
            }
            VerilogDesign design;
            try
            {
                design = source.Part is Part part
                    ? VerilogExporter.Export(part)
                    : VerilogExporter.Export(source.Network, source.Name, source.Ports, source.Cases.Max(@case => SpikeTrace.Run(source.Network, @case.Input, @case.Steps).MostHeld));
            }
            catch (ArgumentException refusal)
            {
                Console.Error.WriteLine(refusal.Message);
                return 1;
            }
            string folder = options.GetValueOrDefault("out", "export");
            List<InputSpikes> inputs = source.Cases.Select(@case => @case.Input).ToList();
            List<int> steps = source.Cases.Select(@case => @case.Steps).ToList();
            string testbench = VerilogTestbench.For(design, inputs, steps);
            string expected = VerilogTestbench.ExpectedOutput(source.Network, inputs, steps);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, design.Name + ".v"), design.Module);
            File.WriteAllText(Path.Combine(folder, design.Name + "_tb.v"), testbench);
            File.WriteAllText(Path.Combine(folder, design.Name + "_expected.txt"), expected);
            Console.WriteLine($"Wrote {design.Name}.v, {design.Name}_tb.v and {design.Name}_expected.txt to {folder}: {design.CounterWidth}-bit counters, {inputs.Count} case(s).");
            return Switch(options, "check", true) ? CoSimulate(design, testbench, expected, folder, inputs.Count) : 0;
        }

        private static int CoSimulate(VerilogDesign design, string testbench, string expected, string folder, int cases)
        {
            if (!Iverilog.IsInstalled)
            {
                Console.WriteLine($"Not co-simulated: {Iverilog.Missing}");
                return 0;
            }
            string simulated = Iverilog.Simulate(design, testbench, folder);
            if (simulated != expected)
            {
                Console.Error.WriteLine($"iverilog differs from our engine; compare its output with {design.Name}_expected.txt.");
                File.WriteAllText(Path.Combine(folder, design.Name + "_simulated.txt"), simulated);
                return Differs;
            }
            Console.WriteLine($"iverilog matches our engine on every step of every neuron in all {cases} case(s).");
            return 0;
        }

        internal static int Nir(IReadOnlyDictionary<string, string> options)
        {
            if (Read(options) is not Source source)
            {
                return 1;
            }
            string folder = options.GetValueOrDefault("out", "export");
            NirDescription description;
            try
            {
                description = NirExporter.Export(source.Network, source.Name, source.Ports, source.Cases);
            }
            catch (ArgumentException refusal)
            {
                Console.Error.WriteLine(refusal.Message);
                return 1;
            }
            Directory.CreateDirectory(folder);
            string stem = PartLibraryFiles.Stem(description.Name);
            File.WriteAllText(Path.Combine(folder, stem + ".nir.json"), NirExporter.ToJson(description));
            if (NirExporter.Unavailable() is string reason)
            {
                Console.WriteLine($"Wrote {stem}.nir.json to {folder}. Not converted to NIR or co-simulated: {reason}");
                return 0;
            }
            try
            {
                string report = NirExporter.WriteAndCheck(description, folder);
                Console.WriteLine($"Wrote {stem}.nir.json and {stem}.nir to {folder}.");
                Console.Write(report);
                return 0;
            }
            catch (InvalidOperationException failure)
            {
                Console.Error.WriteLine(failure.Message);
                return Differs;
            }
        }

        // Writes NAME.xml and NAME.q, and model-checks them with verifyta when it is installed: exits with 3 when a query fails.
        internal static int Uppaal(IReadOnlyDictionary<string, string> options)
        {
            if (Read(options) is not Source source)
            {
                return 1;
            }
            if (source.Part is not Part part)
            {
                Console.Error.WriteLine("export-uppaal needs --part FILE, since its queries come from the part's contract.");
                return 1;
            }
            UppaalModel model;
            try
            {
                model = UppaalExporter.Export(part);
            }
            catch (ArgumentException refusal)
            {
                Console.Error.WriteLine(refusal.Message);
                return 1;
            }
            string folder = options.GetValueOrDefault("out", "export");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, model.Name + ".xml"), model.Model);
            File.WriteAllText(Path.Combine(folder, model.Name + ".q"), model.Queries);
            Console.WriteLine($"Wrote {model.Name}.xml and {model.Name}.q to {folder}: {model.QueryNames.Count} queries over {part.Contract.Cases.Count} case(s).");
            return Switch(options, "check", true) ? ModelCheck(model, folder) : 0;
        }

        private static int ModelCheck(UppaalModel model, string folder)
        {
            if (!Verifyta.IsInstalled)
            {
                Console.WriteLine($"Not model-checked: {Verifyta.Missing}");
                return 0;
            }
            IReadOnlyList<bool> verdicts;
            try
            {
                verdicts = Verifyta.Check(model, folder);
            }
            catch (InvalidOperationException failure)
            {
                Console.Error.WriteLine(failure.Message);
                return 1;
            }
            for (int query = 0; query < verdicts.Count; query++)
            {
                Console.WriteLine($"{(verdicts[query] ? "holds" : "FAILS")}: {model.QueryNames[query]}");
            }
            return verdicts.All(holds => holds) ? 0 : Differs;
        }

        // The part or network to export, or null with the reason on stderr.
        private static Source? Read(IReadOnlyDictionary<string, string> options)
        {
            int steps = (int)Number(options, "steps", 50);
            try
            {
                if (options.GetValueOrDefault("part") is string partFile)
                {
                    Part part = PartLibraryFiles.Read(File.ReadAllText(partFile), Path.GetFileName(partFile)).Part;
                    return new Source(part.Contract.Name, part.Network, part, NetworkPort.ForPart(part), SpikeTrace.Cases(part.Contract));
                }
                if (options.GetValueOrDefault("network") is string networkFile)
                {
                    Network network = NetworkFiles.Load(networkFile) ?? throw new InvalidDataException($"{networkFile} holds no network.");
                    return new Source(Path.GetFileNameWithoutExtension(networkFile), network, null, NetworkPort.Plain(network), new[] { ("no input", InputSpikes.None, steps) });
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
            {
                Console.Error.WriteLine(exception.Message);
                return null;
            }
            Console.Error.WriteLine(CommandLine.Usage);
            Console.Error.WriteLine("Export needs --part FILE (a library part) or --network FILE (a saved network).");
            return null;
        }
    }
}
