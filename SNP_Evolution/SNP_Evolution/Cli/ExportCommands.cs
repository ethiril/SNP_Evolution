using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Export;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;
using static SnpEvolution.Cli.CommandOptions;

namespace SnpEvolution.Cli
{
    // export-verilog and export-nir: write a library part (--part FILE) or a saved network (--network FILE) for hardware,
    // then co-simulate the export against our engine when the tools are installed. A part is checked on every contract
    // case; a network runs for --steps steps with no input. Exits with 1 for a bad command or a network that cannot be
    // exported, and 3 when the co-simulation differs.
    internal static class ExportCommands
    {
        internal const int Differs = 3;

        private sealed record Source(string Name, Network Network, Part? Part, int Steps);

        internal static int Verilog(IReadOnlyDictionary<string, string> options)
        {
            if (Read(options) is not Source source)
            {
                return 1;
            }
            string folder = options.GetValueOrDefault("out", "export");
            VerilogDesign design;
            List<InputSpikes> inputs;
            List<int> steps;
            try
            {
                if (source.Part is Part part)
                {
                    design = VerilogExporter.Export(part);
                    var cases = SpikeTrace.Cases(part.Contract);
                    inputs = cases.Select(@case => @case.Input).ToList();
                    steps = cases.Select(@case => @case.Steps).ToList();
                }
                else
                {
                    inputs = new List<InputSpikes> { InputSpikes.None };
                    steps = new List<int> { source.Steps };
                    long mostHeld = SpikeTrace.Run(source.Network, InputSpikes.None, source.Steps).MostHeld;
                    design = VerilogExporter.Export(source.Network, source.Name, VerilogExporter.PlainPorts(source.Network), mostHeld);
                }
            }
            catch (ArgumentException refusal)
            {
                Console.Error.WriteLine(refusal.Message);
                return 1;
            }
            Directory.CreateDirectory(folder);
            string testbench = VerilogExporter.Testbench(design, inputs, steps);
            string expected = VerilogExporter.ExpectedOutput(source.Network, inputs, steps);
            File.WriteAllText(Path.Combine(folder, design.Name + ".v"), design.Module);
            File.WriteAllText(Path.Combine(folder, design.Name + "_tb.v"), testbench);
            File.WriteAllText(Path.Combine(folder, design.Name + "_expected.txt"), expected);
            Console.WriteLine($"Wrote {design.Name}.v, {design.Name}_tb.v and {design.Name}_expected.txt to {folder}: {design.CounterWidth}-bit counters, {inputs.Count} case(s).");
            if (!Switch(options, "check", true))
            {
                return 0;
            }
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
            Console.WriteLine($"iverilog matches our engine on every step of every neuron in all {inputs.Count} case(s).");
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
                description = source.Part is Part part
                    ? NirExporter.Export(part)
                    : NirExporter.Export(source.Network, source.Name, VerilogExporter.PlainPorts(source.Network).Select(port => (port.Name, port.Neuron, port.IsInput)).ToList(),
                        new[] { ("no input", InputSpikes.None, source.Steps) });
            }
            catch (ArgumentException refusal)
            {
                Console.Error.WriteLine(refusal.Message);
                return 1;
            }
            Directory.CreateDirectory(folder);
            string stem = NirExporter.FileStem(description.Name);
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

        // The part or network to export, or null with the reason on stderr.
        private static Source? Read(IReadOnlyDictionary<string, string> options)
        {
            int steps = (int)Number(options, "steps", 50);
            try
            {
                if (options.GetValueOrDefault("part") is string partFile)
                {
                    LibraryPart part = PartLibraryFiles.Read(File.ReadAllText(partFile), Path.GetFileName(partFile));
                    return new Source(part.Contract.Name, part.Part.Network, part.Part, steps);
                }
                if (options.GetValueOrDefault("network") is string networkFile)
                {
                    Network network = NetworkFiles.Load(networkFile) ?? throw new InvalidDataException($"{networkFile} holds no network.");
                    return new Source(Path.GetFileNameWithoutExtension(networkFile), network, null, steps);
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
