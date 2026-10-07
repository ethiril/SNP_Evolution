using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;
using SnpEvolution.Evolution.Benchmarking;

namespace SnpEvolution.Cli
{
    // With --advise on the advisor's suggestions are applied first and the options given after them, and with --pilot on a
    // quick pilot picks the algorithm.
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
            if (TargetArgs.Settings(args, SettingOptions.Evolve) is not Settings settings)
            {
                return ExitCode.Usage;
            }
            if (args.Get(Advise, false))
            {
                Advice advice = RunAdvisor.Advise(settings);
                RunAdvisor.Format(advice).ToList().ForEach(Console.WriteLine);
                RunAdvisor.ApplyAll(settings, advice);
                SettingOptions.Apply(settings, args, SettingOptions.Evolve);
            }
            if (args.Get(Pilot, false))
            {
                settings.Algorithm = RunAdvisor.Pilot(settings, RunAdvisor.PilotBudget, Console.WriteLine);
            }
            BenchmarkTask task = settings.SelectedTask;
            Console.WriteLine("Evolving a network for {0} with {1}.", task.Name, settings.Algorithm.Name);
            RunNotes.For(settings, task).ToList().ForEach(Console.WriteLine);
            EvolveResult result = EvolveService.Run(new EvolveRequest(settings, RunSeed.For(CommonOptions.SeedFrom(args)), "TargetNet"), Console.WriteLine);
            return result.Error is string error ? Refuse(error) : result.Solved ? ExitCode.Success : ExitCode.Unsolved;
        }
    }
}
