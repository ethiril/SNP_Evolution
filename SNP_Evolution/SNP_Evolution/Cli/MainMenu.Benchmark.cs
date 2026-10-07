using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Search;
using SnpEvolution.Networks;
using SnpEvolution.Storage;

namespace SnpEvolution.Cli
{
    internal sealed partial class MainMenu
    {
        private void BenchmarkMenu()
        {
            int selection = 0;
            while (ConsoleUi.Choose(settings, "Benchmark", new[] { "Every algorithm on the task suite", "Find the best algorithm for the selected task" }, selection) is int choice)
            {
                selection = choice;
                if (choice == 0)
                {
                    RunBenchmark();
                }
                else
                {
                    SelectAlgorithm();
                }
            }
        }

        private void RunBenchmark()
        {
            Console.Clear();
            BenchmarkSettings benchmark = settings.BenchmarkSettings;
            Console.WriteLine("Runs {0} algorithms on {1} tasks, {2} seeds each, with up to {3} evaluations per run, on the {4} engine.",
                SearchCatalog.FromScratch.Count, TaskSuite.All.Count, benchmark.Seeds, benchmark.EvaluationBudget, settings.Engine.Name);
            if (!ConsoleUi.WaitForEnterOrEscape("Press enter to start, or ESC to go back; this can take a while."))
            {
                return;
            }
            string folder = EvolutionSession.NewOutputFolder();
            Stopwatch stopwatch = Stopwatch.StartNew();
            IReadOnlyList<BenchmarkRow> rows = Benchmark.Run(SearchCatalog.FromScratch, TaskSuite.All, benchmark, Console.WriteLine);
            string table = Benchmark.FormatTable(rows);
            Console.WriteLine("\n{0}\nTime elapsed: {1}", table, stopwatch.Elapsed);
            Directory.CreateDirectory(folder);
            NetworkFiles.SaveText(table, Path.Combine(folder, "benchmark.txt"));
            NetworkFiles.SaveText(Benchmark.FormatCsv(rows), Path.Combine(folder, "benchmark.csv"));
            ConsoleUi.WaitForEnter("Press enter to return to the menu.");
        }

        private void SelectAlgorithm()
        {
            Console.Clear();
            BenchmarkTask task = settings.SelectedTask;
            BenchmarkSettings benchmark = settings.BenchmarkSettings;
            long initialBudget = Math.Max(1, benchmark.EvaluationBudget / 8);
            Console.WriteLine("Successive halving over {0} algorithms for: {1}, starting at {2} evaluations per run.", Catalog.Algorithms.Count, task.Name, initialBudget);
            if (!ConsoleUi.WaitForEnterOrEscape("Press enter to start, or ESC to go back; this can take a while."))
            {
                return;
            }
            SelectionResult<EvolutionSearch> result = AlgorithmSelector.Select(Catalog.Algorithms, task, benchmark, initialBudget, Console.WriteLine);
            Console.WriteLine("\nBest algorithm for {0}: {1}", task.Name, result.Winner.Name);
            if (result.BestFound is Individual best)
            {
                Console.WriteLine("Best network found (fitness {0}, {1}):\n{2}", best.Fitness, best.Description, NetworkNotation.Format(best.Genes));
            }
            ConsoleUi.WaitForEnter("Press enter to continue.");
            if (ConsoleUi.Confirm(settings, $"Use {result.Winner.Name} from now on?"))
            {
                settings.Algorithm = result.Winner;
            }
        }
    }
}
