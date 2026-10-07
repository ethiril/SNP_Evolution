using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;
using SnpEvolution.Search.Benchmarking;

namespace SnpEvolution.Cli
{
    // A finished evolve run: the settings it ran with, where it started and how it went.
    internal sealed record EvolveRun(Settings Used, RunStart Start, string FileStem, EvolveResult Result);

    internal sealed class EvolveCommand : Command
    {
        private static readonly Option<bool> Advise = new Option<bool>("advise", ValueKinds.Switch, "apply the advisor's suggestions before the options given");

        private static readonly Option<bool> Pilot = new Option<bool>("pilot", ValueKinds.Switch, "pick the algorithm with a quick pilot");

        internal static readonly Option<RunStart> Start = new Option<RunStart>("start",
            ValueKinds.Words(("scratch", RunStart.Scratch), ("natural", RunStart.NaturalNumbers), ("even", RunStart.EvenNumbers)),
            "start from a random network, or from the natural or even numbers network; scratch unless given");

        public override string Name => "evolve";

        public override string Summary => "Evolves a network for a target or a suite task and saves the run; exits with 2 when no network solved it.";

        public override IReadOnlyList<Option> Options { get; } =
            new Option[] { CommonOptions.Target, CommonOptions.Kind, CommonOptions.Task, Start, CommonOptions.Seed, Advise, Pilot }.Concat(SettingOptions.Flags(SettingOptions.Evolve)).ToList();

        public override ExitCode Run(CommandArgs args) => Run(args, null);

        // finished hears of the run once its files are saved, so the menu can offer to save it for later.
        internal ExitCode Run(CommandArgs args, Action<EvolveRun>? finished)
        {
            RunStart start = args.Get(Start, RunStart.Scratch);
            if (start != RunStart.Scratch && args.Find(CommonOptions.Task) != null)
            {
                return Refuse($"{Start.Flag} natural and even evolve towards a {CommonOptions.Target.Flag}, not a {CommonOptions.Task.Flag}.");
            }
            if (Configured(args, Console.WriteLine) is not Settings settings)
            {
                return ExitCode.Usage;
            }
            if (args.Get(Pilot, false))
            {
                settings.Algorithm = RunAdvisor.Pilot(settings, RunAdvisor.PilotBudget, Console.WriteLine);
            }
            BenchmarkTask task = EvolveService.TaskFor(settings, start);
            Console.WriteLine(start == RunStart.Scratch ? "Evolving a network for {0} with {1}." : "Evolving the {2} network for {0} with {1}.",
                task.Name, settings.Algorithm.Name, EvolveService.Title(start));
            if (start == RunStart.Scratch && settings.Algorithm.EvolvesRulesOnly)
            {
                Console.WriteLine("{0} keeps a random network's structure, so only its rules change; {1} evolves both.", settings.Algorithm.Name, Catalog.StructuralDefault.Name);
            }
            RunNotes.For(settings, task).ToList().ForEach(Console.WriteLine);
            string fileStem = FileStem(start);
            Settings used = settings.Copy();
            EvolveResult result = EvolveService.Run(new EvolveRequest(settings, RunSeed.For(CommonOptions.SeedFrom(args)), fileStem, start), Console.WriteLine);
            if (result.Run != null)
            {
                finished?.Invoke(new EvolveRun(used, start, fileStem, result));
            }
            return Ended(result);
        }

        // With --advise on, the options given override the advisor's suggestions; null when there is nothing to evolve for.
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

        private static string FileStem(RunStart start) => start switch
        {
            RunStart.NaturalNumbers => "NatNumsNet",
            RunStart.EvenNumbers => "EvenNumsNet",
            _ => "TargetNet",
        };
    }
}
