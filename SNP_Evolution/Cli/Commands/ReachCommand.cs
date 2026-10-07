using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;

namespace SnpEvolution.Cli
{
    internal sealed class ReachCommand : Command
    {
        private static readonly Option<string[]> Setups = new Option<string[]>("setups", ValueKinds.List, "the setups to compare; all unless given",
            string.Join(",", ReachService.Setups.Select(setup => setup.Name)));

        private static readonly Option<int> Seeds = new Option<int>("seeds", ValueKinds.PositiveInt, "runs each setup on seeds 1 to N; 10 unless given");

        private static readonly Option<bool> ChargeParts = new Option<bool>("charge-parts", ValueKinds.Switch, "take the part library's cost off composition search's budget; on unless given");

        public override string Name => "reach";

        public override string Summary => "Runs each setup on seeds 1 to N with the same budget and compares how far into the target they get.";

        public override IReadOnlyList<Option> Options { get; } =
            new Option[] { CommonOptions.Target, CommonOptions.Kind, Setups, Seeds, ChargeParts }.Concat(SettingOptions.Flags(SettingOptions.Evolve)).ToList();

        public override IReadOnlyList<Option> Required { get; } = new Option[] { CommonOptions.Target, SettingOptions.Evaluations.Option };

        public override ExitCode Run(CommandArgs args)
        {
            if (TargetArgs.Settings(args, SettingOptions.Evolve) is not Settings settings)
            {
                return ExitCode.Usage;
            }
            IReadOnlyList<ReachService.Setup> setups = ReachService.Setups;
            if (args.Find(Setups) is string[] names)
            {
                if (names.FirstOrDefault(name => ReachService.Setups.All(setup => setup.Name != name)) is string unknown)
                {
                    return Refuse($"No setup is called '{unknown}'. The setups are: {string.Join(", ", ReachService.Setups.Select(setup => setup.Name))}.");
                }
                setups = names.Select(name => ReachService.Setups.Single(setup => setup.Name == name)).ToList();
            }
            string command = "dotnet run -- " + string.Join(" ", args.Line.Select(arg => arg.Contains(' ') || arg.Contains(',') ? $"\"{arg}\"" : arg));
            return ReachService.Run(settings, setups, args.Get(Seeds, 10), args.Get(ChargeParts, true), command, Console.WriteLine) is string error
                ? Refuse(error)
                : ExitCode.Success;
        }
    }
}
