using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Search;

namespace SnpEvolution.Evolution.Benchmarking
{
    public sealed record SelectionRound(long Budget, IReadOnlyList<BenchmarkRow> Standings, IReadOnlyList<string> Advancing);

    public sealed record SelectionResult(AlgorithmChoice Winner, IReadOnlyList<SelectionRound> Rounds, Individual? BestFound);

    // Picks the best algorithm for a task by successive halving: every candidate gets a small budget on a few seeds,
    // the better half goes through with double the budget, and so on until one is left. Most of the effort goes to
    // the promising candidates, and the winner has also produced the best network seen along the way.
    public static class AlgorithmSelector
    {
        public static SelectionResult Select(
            IReadOnlyList<AlgorithmChoice> candidates, BenchmarkTask task, BenchmarkSettings settings, long initialBudget, Action<string>? progress = null)
        {
            if (candidates.Count == 0)
            {
                throw new ArgumentException("There must be at least one candidate algorithm.", nameof(candidates));
            }
            var remaining = candidates.ToList();
            var rounds = new List<SelectionRound>();
            Individual? bestFound = null;
            long budget = initialBudget;
            int round = 0;
            while (true)
            {
                progress?.Invoke($"Round {round + 1}: {remaining.Count} algorithm(s) with {budget} evaluations each on {settings.Seeds} seed(s).");
                var outcomes = (from algorithm in remaining.AsParallel().AsOrdered()
                                from seed in Enumerable.Range(1 + round * settings.Seeds, settings.Seeds)
                                select Benchmark.RunOnce(algorithm, task, seed, budget, settings)).ToList();
                foreach (Individual best in outcomes.Select(outcome => outcome.Best).OfType<Individual>())
                {
                    bestFound = bestFound == null || Ranking.IsBetter(best, bestFound) ? best : bestFound;
                }
                List<BenchmarkRow> standings = Benchmark.Summarise(outcomes).OrderBy(row => row, Standing).ToList();
                int advancing = remaining.Count == 1 ? 1 : (remaining.Count + 1) / 2;
                remaining = standings.Take(advancing).Select(row => remaining.First(algorithm => algorithm.Name == row.Algorithm)).ToList();
                rounds.Add(new SelectionRound(budget, standings, remaining.Select(algorithm => algorithm.Name).ToList()));
                foreach (BenchmarkRow row in standings)
                {
                    progress?.Invoke($"  {row.Algorithm}: solved {row.Solved}/{row.Runs}, mean best {row.MeanBestFitness:0.###}");
                }
                if (remaining.Count == 1)
                {
                    return new SelectionResult(remaining[0], rounds, bestFound);
                }
                budget *= 2;
                round++;
            }
        }

        // More solved runs first, then fitter, then faster to solve.
        public static readonly IComparer<BenchmarkRow> Standing = Comparer<BenchmarkRow>.Create((first, second) =>
        {
            int bySuccess = second.SuccessRate.CompareTo(first.SuccessRate);
            if (bySuccess != 0)
            {
                return bySuccess;
            }
            int byFitness = second.MeanBestFitness.CompareTo(first.MeanBestFitness);
            return byFitness != 0 ? byFitness : (first.MedianEvaluationsToSolve ?? double.MaxValue).CompareTo(second.MedianEvaluationsToSolve ?? double.MaxValue);
        });
    }
}
