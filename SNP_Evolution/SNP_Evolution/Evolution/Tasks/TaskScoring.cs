using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Evolution.Tasks
{
    // Scoring rules several tasks share, each written once.
    public static class TaskScoring
    {
        // The mean score within each of two groups, such as numbers to accept and numbers to reject, averaged over the
        // groups that have members, so getting everything in one group right and everything in the other wrong earns a
        // half. 0 when there is nothing to score.
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

        // The share of a case's runs that get something right, such as one gap of a sequence; 0 with no runs.
        public static float ShareOfRuns<TRun>(IReadOnlyList<TRun> runs, Func<TRun, bool> right) =>
            runs.Count == 0 ? 0 : (float)runs.Count(right) / runs.Count;

        // How many values from the start match the expected ones before the first that does not.
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
