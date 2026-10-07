using System;
using System.Collections.Generic;
using System.Diagnostics;
using SnpEvolution.Application;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Search;
using SnpEvolution.Networks;

namespace SnpEvolution.Cli
{
    // Both race the searches that start from nothing, as the benchmark and select commands do, on the settings' engine.
    internal static class BenchmarkMenu
    {
        public static void Show(MenuState state)
        {
            int selection = 0;
            while (ConsoleUi.Choose(state.Settings, "Benchmark", new[] { "Every algorithm on the task suite", "Find the best algorithm for the selected task" }, selection) is int choice)
            {
                selection = choice;
                if (choice == 0)
                {
                    RunBenchmark(state.Settings);
                }
                else
                {
                    SelectAlgorithm(state.Settings);
                }
            }
        }

        private static void RunBenchmark(Settings settings)
        {
            Console.Clear();
            BenchmarkSettings benchmark = settings.BenchmarkSettings;
            Console.WriteLine("Runs {0} algorithms on {1} tasks, {2} seeds each, with up to {3} evaluations per run, on the {4} engine.",
                SearchCatalog.FromScratch.Count, TaskSuite.All.Count, benchmark.Seeds, benchmark.EvaluationBudget, settings.Engine.Name);
            if (!ConsoleInput.WaitForEnterOrEscape("Press enter to start, or ESC to go back; this can take a while.") || Plan(settings) is not BenchmarkPlan plan)
            {
                return;
            }
            Stopwatch stopwatch = Stopwatch.StartNew();
            IReadOnlyList<BenchmarkRow> rows = BenchmarkService.Run(plan, TaskSuite.All, Console.WriteLine);
            Console.WriteLine("\n{0}\nTime elapsed: {1}", Benchmark.FormatTable(rows), stopwatch.Elapsed);
            BenchmarkService.Save(rows);
            ConsoleInput.WaitForEnter("Press enter to return to the menu.");
        }

        private static void SelectAlgorithm(Settings settings)
        {
            Console.Clear();
            BenchmarkTask task = settings.SelectedTask;
            Console.WriteLine("Successive halving over {0} algorithms for: {1}, starting at {2} evaluations per run.",
                SearchCatalog.FromScratch.Count, task.Name, BenchmarkService.InitialBudget(settings.BenchmarkSettings));
            if (!ConsoleInput.WaitForEnterOrEscape("Press enter to start, or ESC to go back; this can take a while.") || Plan(settings) is not BenchmarkPlan plan)
            {
                return;
            }
            SelectionResult<ISearch<Individual>> result = BenchmarkService.Select(plan, task, Console.WriteLine);
            Console.WriteLine("\nBest algorithm for {0}: {1}", task.Name, result.Winner.Name);
            if (result.BestFound is Individual best)
            {
                Console.WriteLine("Best network found (fitness {0}, {1}):\n{2}", best.Fitness, best.Description, NetworkNotation.Format(best.Genes));
            }
            ConsoleInput.WaitForEnter("Press enter to continue.");
            if (result.Winner is EvolutionSearch winner && ConsoleUi.Confirm(settings, $"Use {winner.Name} from now on?"))
            {
                settings.Algorithm = winner;
            }
        }

        // Null, after showing why, when composition search's part library cannot be read.
        private static BenchmarkPlan? Plan(Settings settings)
        {
            Loaded<BenchmarkPlan> plan = BenchmarkService.Plan(settings, settings.BenchmarkSettings, SearchCatalog.FromScratch);
            if (plan.Value == null)
            {
                Console.WriteLine(plan.Error);
                ConsoleInput.WaitForEnter("Press enter to return to the menu.");
                return null;
            }
            if (plan.Value.PartsNote != null)
            {
                Console.WriteLine(plan.Value.PartsNote);
            }
            return plan.Value;
        }
    }
}
