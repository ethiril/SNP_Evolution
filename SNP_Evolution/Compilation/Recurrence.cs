using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Compilation
{
    // A sequence whose first values are given and whose later values are a fixed mix of the ones before:
    // value n = c1 * value(n-1) + c2 * value(n-2) + ... + ck * value(n-k), from value Initial.Count + 1 on.
    // Fibonacci is 1,1 then c = (1, 1); powers of two are 1 then c = (2); a constant is one value then c = (1).
    public sealed record Recurrence(IReadOnlyList<int> Initial, IReadOnlyList<int> Coefficients)
    {
        public const int MaxOrder = 3;
        public const int MaxCoefficient = 4;

        public int Order => Coefficients.Count;

        // How many earlier values each new value is made of, counting repeats: c1 + c2 + ... + ck.
        public int Uses => Coefficients.Sum();

        // The first count values.
        public IEnumerable<long> Values(int count)
        {
            var values = new List<long>(Initial.Select(value => (long)value));
            for (int n = values.Count; n < count; n++)
            {
                values.Add(Enumerable.Range(1, Order).Sum(back => Coefficients[back - 1] * values[n - back]));
            }
            return values.Take(count);
        }

        // The smallest recurrence of positive values that gives exactly the target, judged by the neurons it
        // compiles to, or null when none does. At least two values must follow from the rule, so a recurrence is
        // never just the target written out.
        public static Recurrence? Fit(IReadOnlyList<int> target)
        {
            Recurrence? best = null;
            for (int order = 1; order <= MaxOrder; order++)
            {
                foreach (int[] coefficients in CoefficientChoices(order).Where(choice => choice[^1] > 0))
                {
                    for (int initial = order; initial <= target.Count - 2; initial++)
                    {
                        var candidate = new Recurrence(target.Take(initial).ToList(), coefficients);
                        if (candidate.Values(target.Count).SequenceEqual(target.Select(value => (long)value)))
                        {
                            if (best == null || RecurrenceCompiler.NeuronCount(candidate) < RecurrenceCompiler.NeuronCount(best))
                            {
                                best = candidate;
                            }
                            break;
                        }
                    }
                }
            }
            return best;
        }

        public override string ToString()
        {
            IEnumerable<string> terms = Coefficients
                .Select((coefficient, index) => (coefficient, back: index + 1))
                .Where(term => term.coefficient > 0)
                .Select(term => (term.coefficient == 1 ? "" : term.coefficient + "*") + $"g(n-{term.back})");
            return $"g(n) = {string.Join(" + ", terms)}, starting {string.Join(",", Initial)}";
        }

        private static IEnumerable<int[]> CoefficientChoices(int order)
        {
            if (order == 0)
            {
                yield return Array.Empty<int>();
                yield break;
            }
            foreach (int[] rest in CoefficientChoices(order - 1))
            {
                for (int coefficient = 0; coefficient <= MaxCoefficient; coefficient++)
                {
                    yield return rest.Append(coefficient).ToArray();
                }
            }
        }
    }
}
