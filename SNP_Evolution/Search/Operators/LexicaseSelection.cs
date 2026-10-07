using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Specs.Accounting;

namespace SnpEvolution.Search.Operators
{
    // Lexicase selection (Helmuth, Spector and Matheson 2015): each pick goes through the task's checks in a fresh
    // random order, keeping only the networks that do best on each, until one is left. A network that is the only
    // one to get some part of the target right can be picked even when its fitness is low, so networks that are
    // right about different parts all get to breed. Checks are scores, not pass or fail, so "best" allows the
    // median absolute deviation of the check's scores (epsilon-lexicase, La Cava et al. 2016). Tasks without
    // checks fall back to another selection.
    public sealed class LexicaseSelection : IParentSelection
    {
        private readonly IParentSelection fallback;

        public LexicaseSelection(IParentSelection? fallback = null) => this.fallback = fallback ?? new TournamentSelection(3);

        public Func<Individual> Prepare(IReadOnlyList<Individual> ranked, Random random) => Picker(ranked, random) ?? fallback.Prepare(ranked, random);

        // Picks from any scored candidates, networks or programs; null when they have no checks to go through.
        public static Func<T>? Picker<T>(IReadOnlyList<T> ranked, Random random) where T : IScored
        {
            int checks = ranked.Count == 0 ? 0 : ranked.Min(candidate => candidate.Checks.Count);
            if (checks == 0)
            {
                return null;
            }
            float[] epsilon = Enumerable.Range(0, checks).Select(check => MedianAbsoluteDeviation(ranked.Select(candidate => candidate.Checks[check]).ToList())).ToArray();
            int[] order = Enumerable.Range(0, checks).ToArray();
            return () =>
            {
                random.Shuffle(order);
                List<T> pool = ranked.ToList();
                foreach (int check in order)
                {
                    float best = pool.Max(candidate => candidate.Checks[check]);
                    pool = pool.Where(candidate => candidate.Checks[check] >= best - epsilon[check]).ToList();
                    if (pool.Count == 1)
                    {
                        break;
                    }
                }
                return pool[random.Next(pool.Count)];
            };
        }

        private static float MedianAbsoluteDeviation(List<float> scores)
        {
            float median = Statistics.Median(scores);
            return Statistics.Median(scores.Select(score => Math.Abs(score - median)));
        }
    }
}
