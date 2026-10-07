using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;

namespace SnpEvolution.Cli
{
    internal sealed class AdviseCommand : Command
    {
        public override string Name => "advise";

        public override string Summary => "Prints the settings the advisor suggests for a target or a suite task, without evolving.";

        public override IReadOnlyList<Option> Options { get; } =
            new Option[] { CommonOptions.Target, CommonOptions.Kind, CommonOptions.Task }.Concat(SettingOptions.Flags(SettingOptions.Evolve)).ToList();

        public override ExitCode Run(CommandArgs args) => Run(args, null);

        // advised hears the advice, so the menu can offer to apply it.
        internal ExitCode Run(CommandArgs args, Action<Advice>? advised)
        {
            if (TargetArgs.Settings(args, SettingOptions.Evolve) is not Settings settings)
            {
                return ExitCode.Usage;
            }
            Advice advice = RunAdvisor.Advise(settings);
            RunAdvisor.Format(advice).ToList().ForEach(Console.WriteLine);
            advised?.Invoke(advice);
            return ExitCode.Success;
        }
    }
}
