using System;
using System.Collections.Generic;
using SnpEvolution.Application;

namespace SnpEvolution.Cli
{
    // The export commands read a part or a saved network and write files to --out. Each exits with 1 when the input
    // cannot be read or exported, and with 3 when the outside tool's check differs from our engine.
    internal abstract class ExportCommand : Command
    {
        protected const string DefaultFolder = "export";

        public override ExitCode Run(CommandArgs args)
        {
            Loaded<ExportSource>? source = args.Find(CommonOptions.Part) is string part ? ExportService.Part(part)
                : args.Find(CommonOptions.Network) is string network ? ExportService.Network(network, args.Get(CommonOptions.Steps, 50))
                : null;
            if (source == null)
            {
                return Refuse("Export needs --part FILE (a library part) or --network FILE (a saved network).");
            }
            if (source.Value == null)
            {
                return Refuse(source.Error!);
            }
            ExportResult result = Export(source.Value, args, args.Get(CommonOptions.Out, DefaultFolder));
            Console.Write(result.Report);
            if (result.Error != null)
            {
                Console.Error.WriteLine(result.Error);
            }
            return result.Status switch
            {
                ExportStatus.Written => ExitCode.Success,
                ExportStatus.Differs => ExitCode.Differs,
                _ => ExitCode.Usage,
            };
        }

        protected abstract ExportResult Export(ExportSource source, CommandArgs args, string folder);
    }

    internal sealed class ExportVerilogCommand : ExportCommand
    {
        public override string Name => "export-verilog";

        public override string Summary => "Writes a deterministic network as Verilog with a testbench, and co-simulates it under iverilog when installed.";

        public override IReadOnlyList<Option> Options { get; } =
            new Option[] { CommonOptions.Part, CommonOptions.Network, CommonOptions.Steps, CommonOptions.Out, CommonOptions.Check };

        protected override ExportResult Export(ExportSource source, CommandArgs args, string folder) =>
            ExportService.Verilog(source, folder, args.Get(CommonOptions.Check, true));
    }

    internal sealed class ExportNirCommand : ExportCommand
    {
        public override string Name => "export-nir";

        public override string Summary => "Writes a hardware-profile network for tools/snp_nir.py, which makes the NIR file and co-simulates it in norse.";

        public override IReadOnlyList<Option> Options { get; } = new Option[] { CommonOptions.Part, CommonOptions.Network, CommonOptions.Steps, CommonOptions.Out };

        protected override ExportResult Export(ExportSource source, CommandArgs args, string folder) => ExportService.Nir(source, folder);
    }

    // Its queries come from the part's contract, so it takes only a part.
    internal sealed class ExportUppaalCommand : ExportCommand
    {
        public override string Name => "export-uppaal";

        public override string Summary => "Writes a part as Uppaal timed automata with queries for its contract, and model-checks them with verifyta when installed.";

        public override IReadOnlyList<Option> Options { get; } = new Option[] { CommonOptions.Part, CommonOptions.Out, CommonOptions.Check };

        public override IReadOnlyList<Option> Required { get; } = new[] { CommonOptions.Part };

        protected override ExportResult Export(ExportSource source, CommandArgs args, string folder) =>
            ExportService.Uppaal(source.Part!, folder, args.Get(CommonOptions.Check, true));
    }
}
