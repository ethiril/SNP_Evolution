using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;
using SnpEvolution.Search.Benchmarking;

namespace SnpEvolution.Cli
{
    internal sealed class EvolveCommand : Command
    {
        private static readonly Option<bool> Advise = new Option<bool>("advise", ValueKinds.Switch, "apply the advisor's suggestions before the options given");

        private static readonly Option<bool> Pilot = new Option<bool>("pilot", ValueKinds.Switch, "pick the algorithm with a quick pilot");

        public override string Name => "evolve";

        public override string Summary => "Evolves a network from scratch for a target and saves the run; exits with 2 when no network solved it.";

        public override IReadOnlyList<Option> Options { get; } =
            new Option[] { CommonOptions.Target, CommonOptions.Kind, CommonOptions.Seed, Advise, Pilot }.Concat(SettingOptions.Flags(SettingOptions.Evolve)).ToList();

        public override IReadOnlyList<Option> Required { get; } = new[] { CommonOptions.Target };

        public override ExitCode Run(CommandArgs args)
        {
            if (Configured(args, Console.WriteLine) is not Settings settings)
            {
                return ExitCode.Usage;
            }
            if (args.Get(Pilot, false))
            {
                settings.Algorithm = RunAdvisor.Pilot(settings, RunAdvisor.PilotBudget, Console.WriteLine);
            }
            BenchmarkTask task = settings.SelectedTask;
            Console.WriteLine("Evolving a network for {0} with {1}.", task.Name, settings.Algorithm.Name);
            RunNotes.For(settings, task).ToList().ForEach(Console.WriteLine);
            return Ended(EvolveService.Run(new EvolveRequest(settings, RunSeed.For(CommonOptions.SeedFrom(args)), "TargetNet"), Console.WriteLine));
        }

        // With --advise on, the options given override the advisor's suggestions; null when the target is not of its kind.
        internal static Settings? Configured(CommandArgs args, Action<string> log)
        {
            if (TargetArgs.Settings(args, SettingOptions.Evolve) is not Settings settings)
            {
                return null;
            }
            if (args.Get(Advise, false))
            {
                Advice advice = RunAdvisor.Advise(settings);
                RunAdvisor.Format(advice).ToList().ForEach(log);
                RunAdvisor.ApplyAll(settings, advice);
                SettingOptions.Apply(settings, args, SettingOptions.Evolve);
            }
            return settings;
        }
    }
}
