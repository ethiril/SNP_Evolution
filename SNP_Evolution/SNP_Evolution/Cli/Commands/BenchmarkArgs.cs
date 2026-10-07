using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Search;

namespace SnpEvolution.Cli
{
    // The options benchmark and select share: which searches race on which tasks, and with what budget and engine.
    internal static class BenchmarkArgs
    {
        private static readonly SettingOption[] Settings =
        {
            SettingOptions.BenchmarkBudget, SettingOptions.BenchmarkSeeds, SettingOptions.BenchmarkPopulation, SettingOptions.Repetitions,
            SettingOptions.Library, SettingOptions.HandBuilt,
        };

        public static readonly IReadOnlyList<Option> Options = new Option[]
        {
            CommonOptions.Task, CommonOptions.Algorithm, CommonOptions.Sampled, CommonOptions.Configurations, SettingOptions.Lexicase.Option,
        }.Concat(SettingOptions.Flags(Settings)).ToList();

        public static List<BenchmarkTask> Tasks(CommandArgs args) => Catalog.Matching(TaskSuite.All, task => task.Name, args.Find(CommonOptions.Task));

        // Null, with the reason on stderr, when no search matches or the part library cannot be read.
        public static BenchmarkPlan? Plan(CommandArgs args)
        {
            List<ISearch<Individual>> searches = Catalog.Matching(SearchCatalog.FromScratch, search => search.Name, args.Find(CommonOptions.Algorithm));
            if (searches.Count == 0)
            {
                Console.Error.WriteLine($"No search matches '{args.Find(CommonOptions.Algorithm)}'; run 'algorithms' to list them.");
                return null;
            }
            var settings = new Application.Settings();
            SettingOptions.Apply(settings, args, Settings);
            BenchmarkSettings benchmark = settings.BenchmarkSettings with { CreateEngine = CommonOptions.EngineFrom(args).Factory, Lexicase = args.Get(SettingOptions.Lexicase.Typed, false) };
            Loaded<BenchmarkPlan> plan = BenchmarkService.Plan(settings, benchmark, searches);
            if (plan.Value == null)
            {
                Console.Error.WriteLine(plan.Error);
                return null;
            }
            if (plan.Value.PartsNote != null)
            {
                Console.Error.WriteLine(plan.Value.PartsNote);
            }
            return plan.Value;
        }
    }

    internal sealed class BenchmarkCommand : Command
    {
        public override string Name => "benchmark";

        public override string Summary => "Runs every matching search on every matching suite task over several seeds, with a budget of evaluations per run, and prints the table.";

        public override IReadOnlyList<Option> Options => BenchmarkArgs.Options;

        public override ExitCode Run(CommandArgs args)
        {
            List<BenchmarkTask> tasks = BenchmarkArgs.Tasks(args);
            if (tasks.Count == 0)
            {
                return Refuse("No task matches --task; run 'tasks' to list them.");
            }
            if (BenchmarkArgs.Plan(args) is not BenchmarkPlan plan)
            {
                return ExitCode.Usage;
            }
            Console.WriteLine(Benchmark.FormatTable(BenchmarkService.Run(plan, tasks, Console.Error.WriteLine)));
            return ExitCode.Success;
        }
    }

    internal sealed class SelectCommand : Command
    {
        public override string Name => "select";

        public override string Summary => "Picks the best of the matching searches for one suite task by successive halving.";

        public override IReadOnlyList<Option> Options => BenchmarkArgs.Options;

        public override IReadOnlyList<Option> Required { get; } = new[] { CommonOptions.Task };

        public override ExitCode Run(CommandArgs args)
        {
            List<BenchmarkTask> tasks = BenchmarkArgs.Tasks(args);
            if (tasks.Count != 1)
            {
                return Refuse("select needs a --task that matches exactly one task; run 'tasks' to list them.");
            }
            if (BenchmarkArgs.Plan(args) is not BenchmarkPlan plan)
            {
                return ExitCode.Usage;
            }
            SelectionResult<ISearch<Individual>> result = BenchmarkService.Select(plan, tasks[0], Console.WriteLine);
            Console.WriteLine("Best algorithm for {0}: {1}", tasks[0].Name, result.Winner.Name);
            if (result.BestFound is Individual best)
            {
                Console.WriteLine("Best network found (fitness {0}, {1}):\n{2}", best.Fitness, best.Description, Networks.NetworkNotation.Format(best.Genes));
            }
            return ExitCode.Success;
        }
    }
}
