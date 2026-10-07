using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Verification;
using SnpEvolution.Export;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;

namespace SnpEvolution.Application
{
    // A part or network to export. Cases are a part's contract cases, or one run with no input for a network.
    internal sealed record ExportSource(string Name, Network Network, Part? Part, IReadOnlyList<NetworkPort> Ports,
        IReadOnlyList<(string Label, InputSpikes Input, int Steps)> Cases);

    internal enum ExportStatus
    {
        Written,
        Failed,
        Differs,
    }

    // Report is what was written and checked; Error says why the export failed or how the check differed.
    internal sealed record ExportResult(ExportStatus Status, string Report, string? Error = null);

    // Writes a network as Verilog, NIR or Uppaal, and checks the file against our engine with the outside tool when it
    // is installed.
    internal static class ExportService
    {
        public static Loaded<ExportSource> Part(string file)
        {
            Loaded<PartFile> loaded = PartLibraries.ReadPart(file);
            if (loaded.Value == null)
            {
                return Loaded<ExportSource>.Failed(loaded.Error!);
            }
            Part part = loaded.Value.Part.Part;
            return Loaded<ExportSource>.Of(new ExportSource(part.Contract.Name, part.Network, part, NetworkPort.ForPart(part), SpikeTrace.Cases(part.Contract)));
        }

        public static Loaded<ExportSource> Network(string file, int steps)
        {
            try
            {
                Network network = NetworkFiles.Load(file) ?? throw new InvalidDataException($"{file} holds no network.");
                return Loaded<ExportSource>.Of(new ExportSource(Path.GetFileNameWithoutExtension(file), network, null, NetworkPort.Plain(network), new[] { ("no input", InputSpikes.None, steps) }));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or Newtonsoft.Json.JsonException)
            {
                return Loaded<ExportSource>.Failed(exception.Message);
            }
        }

        // Co-simulates under iverilog when check is set and it is installed.
        public static ExportResult Verilog(ExportSource source, string folder, bool check)
        {
            VerilogDesign design;
            try
            {
                design = source.Part is Part part
                    ? VerilogExporter.Export(part)
                    : VerilogExporter.Export(source.Network, source.Name, source.Ports, source.Cases.Max(@case => SpikeTrace.Run(source.Network, @case.Input, @case.Steps).MostHeld));
            }
            catch (ArgumentException refusal)
            {
                return new ExportResult(ExportStatus.Failed, "", refusal.Message);
            }
            List<InputSpikes> inputs = source.Cases.Select(@case => @case.Input).ToList();
            List<int> steps = source.Cases.Select(@case => @case.Steps).ToList();
            string testbench = VerilogTestbench.For(design, inputs, steps);
            string expected = VerilogTestbench.ExpectedOutput(source.Network, inputs, steps);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, design.Name + ".v"), design.Module);
            File.WriteAllText(Path.Combine(folder, design.Name + "_tb.v"), testbench);
            File.WriteAllText(Path.Combine(folder, design.Name + "_expected.txt"), expected);
            var report = new StringBuilder();
            report.AppendLine($"Wrote {design.Name}.v, {design.Name}_tb.v and {design.Name}_expected.txt to {folder}: {design.CounterWidth}-bit counters, {inputs.Count} case(s).");
            if (!check)
            {
                return new ExportResult(ExportStatus.Written, report.ToString());
            }
            if (!Iverilog.IsInstalled)
            {
                return new ExportResult(ExportStatus.Written, report.AppendLine($"Not co-simulated: {Iverilog.Missing}").ToString());
            }
            string simulated = Iverilog.Simulate(design, testbench, folder);
            if (simulated != expected)
            {
                File.WriteAllText(Path.Combine(folder, design.Name + "_simulated.txt"), simulated);
                return new ExportResult(ExportStatus.Differs, report.ToString(), $"iverilog differs from our engine; compare its output with {design.Name}_expected.txt.");
            }
            return new ExportResult(ExportStatus.Written, report.AppendLine($"iverilog matches our engine on every step of every neuron in all {inputs.Count} case(s).").ToString());
        }

        // Writes the network for tools/snp_nir.py, which makes the NIR file and co-simulates it in norse when its tools are installed.
        public static ExportResult Nir(ExportSource source, string folder)
        {
            NirDescription description;
            try
            {
                description = NirExporter.Export(source.Network, source.Name, source.Ports, source.Cases);
            }
            catch (ArgumentException refusal)
            {
                return new ExportResult(ExportStatus.Failed, "", refusal.Message);
            }
            Directory.CreateDirectory(folder);
            string stem = PartLibraryFiles.Stem(description.Name);
            File.WriteAllText(Path.Combine(folder, stem + ".nir.json"), NirExporter.ToJson(description));
            if (NirExporter.Unavailable() is string reason)
            {
                return new ExportResult(ExportStatus.Written, $"Wrote {stem}.nir.json to {folder}. Not converted to NIR or co-simulated: {reason}{Environment.NewLine}");
            }
            try
            {
                string report = NirExporter.WriteAndCheck(description, folder);
                return new ExportResult(ExportStatus.Written, $"Wrote {stem}.nir.json and {stem}.nir to {folder}.{Environment.NewLine}{report}");
            }
            catch (InvalidOperationException failure)
            {
                return new ExportResult(ExportStatus.Differs, "", failure.Message);
            }
        }

        // Writes NAME.xml and NAME.q for a part, and model-checks them with verifyta when check is set and it is installed.
        public static ExportResult Uppaal(Part part, string folder, bool check)
        {
            UppaalModel model;
            try
            {
                model = UppaalExporter.Export(part);
            }
            catch (ArgumentException refusal)
            {
                return new ExportResult(ExportStatus.Failed, "", refusal.Message);
            }
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, model.Name + ".xml"), model.Model);
            File.WriteAllText(Path.Combine(folder, model.Name + ".q"), model.Queries);
            var report = new StringBuilder();
            report.AppendLine($"Wrote {model.Name}.xml and {model.Name}.q to {folder}: {model.QueryNames.Count} queries over {part.Contract.Cases.Count} case(s).");
            if (!check)
            {
                return new ExportResult(ExportStatus.Written, report.ToString());
            }
            if (!Verifyta.IsInstalled)
            {
                return new ExportResult(ExportStatus.Written, report.AppendLine($"Not model-checked: {Verifyta.Missing}").ToString());
            }
            IReadOnlyList<bool> verdicts;
            try
            {
                verdicts = Verifyta.Check(model, folder);
            }
            catch (InvalidOperationException failure)
            {
                return new ExportResult(ExportStatus.Failed, report.ToString(), failure.Message);
            }
            for (int query = 0; query < verdicts.Count; query++)
            {
                report.AppendLine($"{(verdicts[query] ? "holds" : "FAILS")}: {model.QueryNames[query]}");
            }
            return new ExportResult(verdicts.All(holds => holds) ? ExportStatus.Written : ExportStatus.Differs, report.ToString());
        }
    }
}
