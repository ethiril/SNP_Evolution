using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Evolution.Benchmarking
{
    // U is the Mann-Whitney statistic of the first sample, the number of pairs in which it is larger, ties counting a
    // half. A12 is the Vargha-Delaney effect size, U over all pairs: the chance a run from the first sample beats one
    // from the second, 0.5 for no difference. P is two-sided, exact when Exact says so.
    public sealed record MannWhitneyResult(double U, double P, double A12, bool Exact);

    public static class Statistics
    {
        // The most ways of splitting the pooled sample enumerated for an exact p-value; past it the normal
        // approximation, with a correction for ties, is used.
        private const long MaxExactSplits = 2_000_000;

        public static MannWhitneyResult MannWhitney(IReadOnlyList<double> first, IReadOnlyList<double> second)
        {
            int n1 = first.Count, n2 = second.Count;
            if (n1 == 0 || n2 == 0)
            {
                throw new ArgumentException("Both samples need at least one value.");
            }
            double[] ranks = MidRanks(first.Concat(second).ToList());
            double u = ranks.Take(n1).Sum() - n1 * (n1 + 1) / 2.0;
            double a12 = u / (n1 * (double)n2);
            if (Choose(n1 + n2, n1) <= MaxExactSplits)
            {
                return new MannWhitneyResult(u, ExactP(ranks, n1, u), a12, true);
            }
            int n = n1 + n2;
            double ties = first.Concat(second).GroupBy(value => value).Sum(group => Math.Pow(group.Count(), 3) - group.Count());
            double variance = n1 * (double)n2 / 12 * (n + 1 - ties / (n * (double)(n - 1)));
            double mean = n1 * (double)n2 / 2;
            double z = variance == 0 ? 0 : (Math.Abs(u - mean) - 0.5) / Math.Sqrt(variance);
            return new MannWhitneyResult(u, Math.Min(1, 2 * (1 - NormalCdf(Math.Max(0, z)))), a12, false);
        }

        public static double Median(IEnumerable<double> values)
        {
            List<double> sorted = values.OrderBy(value => value).ToList();
            if (sorted.Count == 0)
            {
                return double.NaN;
            }
            int middle = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
        }

        // Ranks from 1, with tied values sharing the mean of the ranks they span.
        private static double[] MidRanks(IReadOnlyList<double> values)
        {
            int[] order = Enumerable.Range(0, values.Count).OrderBy(index => values[index]).ToArray();
            var ranks = new double[values.Count];
            for (int start = 0; start < order.Length;)
            {
                int end = start;
                while (end + 1 < order.Length && values[order[end + 1]] == values[order[start]])
                {
                    end++;
                }
                for (int index = start; index <= end; index++)
                {
                    ranks[order[index]] = (start + end) / 2.0 + 1;
                }
                start = end + 1;
            }
            return ranks;
        }

        // The share of every way of choosing n1 of the pooled ranks whose U is at least as far from the middle as the
        // one observed, which is exact with ties since the ranks are the mid-ranks actually seen.
        private static double ExactP(double[] ranks, int n1, double observed)
        {
            int n2 = ranks.Length - n1;
            double middle = n1 * (double)n2 / 2, distance = Math.Abs(observed - middle) - 1e-9;
            long extreme = 0, total = 0;
            void Walk(int next, int chosen, double sum)
            {
                if (chosen == n1)
                {
                    total++;
                    extreme += Math.Abs(sum - n1 * (n1 + 1) / 2.0 - middle) >= distance ? 1 : 0;
                    return;
                }
                for (int index = next; index <= ranks.Length - (n1 - chosen); index++)
                {
                    Walk(index + 1, chosen + 1, sum + ranks[index]);
                }
            }
            Walk(0, 0, 0);
            return (double)extreme / total;
        }

        private static double Choose(int n, int k)
        {
            double result = 1;
            for (int index = 1; index <= k; index++)
            {
                result = result * (n - k + index) / index;
            }
            return result;
        }

        // Abramowitz and Stegun 26.2.17, good to about 1e-7.
        private static double NormalCdf(double z)
        {
            double t = 1 / (1 + 0.2316419 * Math.Abs(z));
            double density = Math.Exp(-z * z / 2) / Math.Sqrt(2 * Math.PI);
            double tail = density * t * (0.319381530 + t * (-0.356563782 + t * (1.781477937 + t * (-1.821255978 + t * 1.330274429))));
            return z >= 0 ? 1 - tail : tail;
        }
    }
}
