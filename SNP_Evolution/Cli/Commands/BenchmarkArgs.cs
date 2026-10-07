using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;
using SnpEvolution.Search;
using SnpEvolution.Search.Benchmarking;
using SnpEvolution.Search.Fitness;

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
            Application.Settings settings = args.StartingSettings();
            SettingOptions.Apply(settings, args, Settings.Append(SettingOptions.Lexicase));
            BenchmarkSettings benchmark = settings.BenchmarkSettings with { CreateEngine = CommonOptions.EngineFrom(args).Factory, Lexicase = settings.Lexicase };
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

        public override ExitCode Run(CommandArgs args) => Run(args, null);

        // done hears the table's rows, so the menu can save them.
        internal ExitCode Run(CommandArgs args, Action<IReadOnlyList<BenchmarkRow>>? done)
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
            IReadOnlyList<BenchmarkRow> rows = BenchmarkService.Run(plan, tasks, Console.Error.WriteLine);
            Console.WriteLine(Benchmark.FormatTable(rows));
            done?.Invoke(rows);
            return ExitCode.Success;
        }
    }

    internal sealed class SelectCommand : Command
    {
        public override string Name => "select";

        public override string Summary => "Picks the best of the matching searches for one suite task by successive halving.";

        public override IReadOnlyList<Option> Options => BenchmarkArgs.Options;

        public override IReadOnlyList<Option> Required { get; } = new[] { CommonOptions.Task };

        public override ExitCode Run(CommandArgs args) => Run(args, null);

        // picked hears the winner, so the menu can offer to evolve with it.
        internal ExitCode Run(CommandArgs args, Action<ISearch<Individual>>? picked)
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
                Console.WriteLine("Best network found (fitness {0}, {1}):\n{2}", best.Fitness, best.Description, Model.NetworkNotation.Format(best.Genes));
            }
            picked?.Invoke(result.Winner);
            return ExitCode.Success;
        }
    }
}
