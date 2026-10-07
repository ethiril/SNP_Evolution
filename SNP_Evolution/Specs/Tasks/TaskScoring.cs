using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Specs.Tasks
{
    public static class TaskScoring
    {
        // Averaging per group means getting one group all right and the other all wrong earns only a half.
        public static float BalancedAccuracy<T>(IEnumerable<T> items, Func<T, bool> group, Func<T, float> score)
        {
            List<T> all = items.ToList();
            float[] accuracy = new[] { true, false }
                .Select(member => all.Where(item => group(item) == member).Select(score).ToList())
                .Where(scores => scores.Count > 0)
                .Select(scores => scores.Average())
                .ToArray();
            return accuracy.Length == 0 ? 0 : accuracy.Average();
        }

        public static float ShareOfRuns<TRun>(IReadOnlyList<TRun> runs, Func<TRun, bool> right) =>
            runs.Count == 0 ? 0 : (float)runs.Count(right) / runs.Count;

        public static int CorrectPrefix<T>(IReadOnlyList<T> actual, IReadOnlyList<T> expected)
        {
            int prefix = 0;
            while (prefix < expected.Count && prefix < actual.Count && EqualityComparer<T>.Default.Equals(actual[prefix], expected[prefix]))
            {
                prefix++;
            }
            return prefix;
        }
    }
}
