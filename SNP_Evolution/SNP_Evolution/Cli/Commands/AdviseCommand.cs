using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;

namespace SnpEvolution.Cli
{
    internal sealed class AdviseCommand : Command
    {
        public override string Name => "advise";

        public override string Summary => "Prints the settings the advisor suggests for a target, without evolving.";

        public override IReadOnlyList<Option> Options { get; } =
            new Option[] { CommonOptions.Target, CommonOptions.Kind }.Concat(SettingOptions.Flags(SettingOptions.Evolve)).ToList();

        public override IReadOnlyList<Option> Required { get; } = new[] { CommonOptions.Target };

        public override ExitCode Run(CommandArgs args)
        {
            if (TargetArgs.Settings(args, SettingOptions.Evolve) is not Settings settings)
            {
                return ExitCode.Usage;
            }
            RunAdvisor.Format(RunAdvisor.Advise(settings)).ToList().ForEach(Console.WriteLine);
            return ExitCode.Success;
        }
    }
}
