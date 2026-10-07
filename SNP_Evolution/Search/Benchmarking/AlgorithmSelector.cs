using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Search;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Fitness;

namespace SnpEvolution.Search.Benchmarking
{
    public sealed record SelectionRound(long Budget, IReadOnlyList<BenchmarkRow> Standings, IReadOnlyList<string> Advancing);

    public sealed record SelectionResult<TSearch>(TSearch Winner, IReadOnlyList<SelectionRound> Rounds, Individual? BestFound);

    // Picks the best algorithm for a task by successive halving: every candidate gets a small budget on a few seeds,
    // the better half goes through with double the budget, and so on until one is left. Most of the effort goes to
    // the promising candidates, and the winner has also produced the best network seen along the way.
    public static class AlgorithmSelector
    {
        public static SelectionResult<TSearch> Select<TSearch>(
            IReadOnlyList<TSearch> candidates, BenchmarkTask task, BenchmarkSettings settings, long initialBudget, Action<string>? progress = null)
            where TSearch : ISearch<Individual>
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
                int first = 1 + round * settings.Seeds;
                var runs = remaining.AsParallel().AsOrdered()
                    .Select(search => (Search: search, Outcomes: Enumerable.Range(first, settings.Seeds).Select(seed => Benchmark.RunOnce(search, task, seed, budget, settings)).ToList()))
                    .ToList();
                foreach (Individual best in runs.SelectMany(run => run.Outcomes).Select(outcome => outcome.Best).OfType<Individual>())
                {
                    bestFound = bestFound == null || Ranking.IsBetter(best, bestFound) ? best : bestFound;
                }
                var ranked = runs.Select(run => (run.Search, Row: Benchmark.Summarise(run.Outcomes).Single())).OrderBy(run => run.Row, Standing).ToList();
                List<BenchmarkRow> standings = ranked.Select(run => run.Row).ToList();
                int advancing = remaining.Count == 1 ? 1 : (remaining.Count + 1) / 2;
                remaining = ranked.Take(advancing).Select(run => run.Search).ToList();
                rounds.Add(new SelectionRound(budget, standings, remaining.Select(search => search.Name).ToList()));
                foreach (BenchmarkRow row in standings)
                {
                    progress?.Invoke($"  {row.Algorithm}: solved {row.Solved}/{row.Runs}, mean best {row.MeanBestFitness:0.###}");
                }
                if (remaining.Count == 1)
                {
                    return new SelectionResult<TSearch>(remaining[0], rounds, bestFound);
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
