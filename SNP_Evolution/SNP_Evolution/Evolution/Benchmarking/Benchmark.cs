using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution.Benchmarking
{
    // How every benchmark run is set up. Budgets are counted in network evaluations rather than time, so results
    // compare fairly across machines and engines.
    public sealed record BenchmarkSettings(
        int Seeds,
        long EvaluationBudget,
        int PopulationSize,
        float MutationRate,
        int MaxSteps,
        int Repetitions,
        GenomeSpace Space,
        IReadOnlyList<string> Templates,
        int MaxSpikeGroupSize,
        Func<ISimulationEngine> CreateEngine,
        IReadOnlyList<LibraryPart>? Parts = null)
    {
        public static BenchmarkSettings Default { get; } = new BenchmarkSettings(
            Seeds: 5,
            EvaluationBudget: 5_000,
            PopulationSize: 40,
            MutationRate: 0.5f,
            MaxSteps: 50,
            Repetitions: 50,
            Space: new GenomeSpace(),
            Templates: ExpressionGenerator.ExperimentalTemplates,
            MaxSpikeGroupSize: 4,
            CreateEngine: () => new ExhaustiveCpuEngine());
    }

    public sealed record RunOutcome(string Algorithm, string Task, int Seed, bool Solved, long Evaluations, float BestFitness, Individual? Best);

    // One algorithm on one task, summarised over every seed.
    public sealed record BenchmarkRow(string Algorithm, string Task, int Runs, int Solved, double? MedianEvaluationsToSolve, double MeanBestFitness, double? MeanSolvedSize)
    {
        public double SuccessRate => Runs == 0 ? 0 : (double)Solved / Runs;
    }

    public static class Benchmark
    {
        // Runs the algorithm on the task until it is solved or the budget is spent.
        public static RunOutcome RunOnce(AlgorithmChoice algorithm, BenchmarkTask task, int seed, long budget, BenchmarkSettings settings)
        {
            var random = new Random(seed);
            GenomeSpace space = settings.Space with { InputCount = task.Task.InputCount, RuleForm = task.RuleForm };
            var factory = new NetworkFactory(space, new ExpressionGenerator(settings.Templates, settings.MaxSpikeGroupSize, random), random);
            var evaluator = new FitnessEvaluator(
                settings.CreateEngine(), task.Task, new SimulationOptions(settings.MaxSteps, settings.Repetitions, task.Timing), solvedRetestCount: 5, random);
            // Each run gets a library of its own, since copies and credit are counted in it.
            IGeneticAlgorithm run = algorithm.Create(new EvolutionContext(
                settings.PopulationSize, settings.MutationRate, random, factory.NewNetwork, evaluator, factory, _ => { },
                Parts: settings.Parts is { Count: > 0 } parts ? ModuleLibrary.Of(parts) : null));
            while (evaluator.Evaluations < budget)
            {
                run.NextGeneration();
                if (run.Best is Individual best && FitnessEvaluator.IsSolvingFitness(best.Fitness) && evaluator.IsReliablySolved(best.Genes))
                {
                    return new RunOutcome(algorithm.Name, task.Name, seed, true, evaluator.Evaluations, best.Fitness, best);
                }
            }
            return new RunOutcome(algorithm.Name, task.Name, seed, false, evaluator.Evaluations, run.Best?.Fitness ?? 0, run.Best);
        }

        // Every algorithm on every task over every seed, run in parallel. Seeds are shared between algorithms, so
        // each one starts from the same random state on the same task.
        public static IReadOnlyList<BenchmarkRow> Run(
            IReadOnlyList<AlgorithmChoice> algorithms, IReadOnlyList<BenchmarkTask> tasks, BenchmarkSettings settings, Action<string>? progress = null)
        {
            var jobs = (from algorithm in algorithms from task in tasks from seed in Enumerable.Range(1, settings.Seeds) select (algorithm, task, seed)).ToList();
            var outcomes = new RunOutcome[jobs.Count];
            int finished = 0;
            Parallel.For(0, jobs.Count, index =>
            {
                (AlgorithmChoice algorithm, BenchmarkTask task, int seed) = jobs[index];
                outcomes[index] = RunOnce(algorithm, task, seed, settings.EvaluationBudget, settings);
                progress?.Invoke($"[{Interlocked.Increment(ref finished)}/{jobs.Count}] {algorithm.Name} on {task.Name}, seed {seed}: "
                    + (outcomes[index].Solved ? $"solved in {outcomes[index].Evaluations} evaluations" : $"best {outcomes[index].BestFitness:0.###}"));
            });
            return Summarise(outcomes);
        }

        public static IReadOnlyList<BenchmarkRow> Summarise(IEnumerable<RunOutcome> outcomes) =>
            outcomes
                .GroupBy(outcome => (outcome.Algorithm, outcome.Task))
                .Select(group =>
                {
                    List<RunOutcome> solved = group.Where(outcome => outcome.Solved).ToList();
                    return new BenchmarkRow(
                        group.Key.Algorithm,
                        group.Key.Task,
                        group.Count(),
                        solved.Count,
                        solved.Count == 0 ? null : Median(solved.Select(outcome => (double)outcome.Evaluations)),
                        group.Average(outcome => outcome.BestFitness),
                        solved.Count == 0 ? null : solved.Average(outcome => outcome.Best!.Genes.Size));
                })
                .ToList();

        public static string FormatTable(IReadOnlyList<BenchmarkRow> rows)
        {
            var table = new List<string[]> { new[] { "Task", "Algorithm", "Solved", "Median evals", "Mean best", "Mean size" } };
            table.AddRange(rows.OrderBy(row => row.Task).ThenByDescending(row => row.SuccessRate).ThenBy(row => row.MedianEvaluationsToSolve ?? double.MaxValue)
                .Select(row => new[]
                {
                    row.Task,
                    row.Algorithm,
                    $"{row.Solved}/{row.Runs}",
                    row.MedianEvaluationsToSolve?.ToString("0", CultureInfo.InvariantCulture) ?? "-",
                    row.MeanBestFitness.ToString("0.000", CultureInfo.InvariantCulture),
                    row.MeanSolvedSize?.ToString("0", CultureInfo.InvariantCulture) ?? "-",
                }));
            int[] widths = Enumerable.Range(0, table[0].Length).Select(column => table.Max(row => row[column].Length)).ToArray();
            var text = new StringBuilder();
            foreach (string[] row in table)
            {
                text.AppendLine(string.Join("   ", row.Select((cell, column) => cell.PadRight(widths[column]))).TrimEnd());
            }
            return text.ToString();
        }

        public static string FormatCsv(IReadOnlyList<BenchmarkRow> rows)
        {
            var text = new StringBuilder("task,algorithm,runs,solved,median_evaluations_to_solve,mean_best_fitness,mean_solved_size\n");
            foreach (BenchmarkRow row in rows)
            {
                text.AppendLine(string.Join(",",
                    Quote(row.Task), Quote(row.Algorithm), row.Runs, row.Solved,
                    row.MedianEvaluationsToSolve?.ToString(CultureInfo.InvariantCulture) ?? "",
                    row.MeanBestFitness.ToString(CultureInfo.InvariantCulture),
                    row.MeanSolvedSize?.ToString(CultureInfo.InvariantCulture) ?? ""));
            }
            return text.ToString();
        }

        private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

        private static double Median(IEnumerable<double> values)
        {
            List<double> sorted = values.OrderBy(value => value).ToList();
            int middle = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
        }
    }
}
