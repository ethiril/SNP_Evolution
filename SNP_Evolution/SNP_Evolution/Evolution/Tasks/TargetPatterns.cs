using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Evolution.Tasks
{
    // A value in a target that breaks a pattern every other value follows, and what it would be if it did not.
    public sealed record SuspectedTypo(int Index, int Found, int Expected, string Pattern);

    // Spots a likely typo in a sequence: a single value that breaks a pattern the rest follow, such as 114 in the
    // Fibonacci numbers where 144 belongs. Only checked on sequences long enough for the pattern to be convincing.
    public static class TargetPatterns
    {
        public const int MinimumLength = 5;

        private sealed record Pattern(string Name, Func<IReadOnlyList<int>, bool> Holds, Func<IReadOnlyList<int>, int, int?> Repair);

        private static readonly Pattern[] Patterns =
        {
            new Pattern("each value is the sum of the two before it", SumOfTwoHolds, SumOfTwoRepair),
            new Pattern("the values go up by the same step", ArithmeticHolds, ArithmeticRepair),
            new Pattern("each value is the one before times the same number", GeometricHolds, GeometricRepair),
        };

        public static SuspectedTypo? Find(IReadOnlyList<int> values)
        {
            if (values.Count < MinimumLength)
            {
                return null;
            }
            foreach (Pattern pattern in Patterns.Where(pattern => !pattern.Holds(values)))
            {
                for (int index = 0; index < values.Count; index++)
                {
                    if (pattern.Repair(values, index) is int repaired && repaired > 0 && repaired != values[index]
                        && pattern.Holds(values.Select((value, position) => position == index ? repaired : value).ToList()))
                    {
                        return new SuspectedTypo(index, values[index], repaired, pattern.Name);
                    }
                }
            }
            return null;
        }

        private static bool SumOfTwoHolds(IReadOnlyList<int> values) =>
            Enumerable.Range(2, values.Count - 2).All(index => values[index] == values[index - 1] + values[index - 2]);

        private static int? SumOfTwoRepair(IReadOnlyList<int> values, int index) => index switch
        {
            >= 2 => values[index - 1] + values[index - 2],
            1 => values[3] - values[2],
            _ => values[2] - values[1],
        };

        private static bool ArithmeticHolds(IReadOnlyList<int> values) =>
            Enumerable.Range(2, values.Count - 2).All(index => values[index] - values[index - 1] == values[1] - values[0]);

        private static int? ArithmeticRepair(IReadOnlyList<int> values, int index) => index switch
        {
            0 => 2 * values[1] - values[2],
            _ when index == values.Count - 1 => 2 * values[index - 1] - values[index - 2],
            _ => (values[index - 1] + values[index + 1]) % 2 == 0 ? (values[index - 1] + values[index + 1]) / 2 : null,
        };

        private static bool GeometricHolds(IReadOnlyList<int> values) =>
            values[1] % values[0] == 0 && values[1] / values[0] >= 2
            && Enumerable.Range(2, values.Count - 2).All(index => values[index] == values[index - 1] * (values[1] / values[0]));

        // The ratio is read from a pair of values that does not involve the suspect.
        private static int? GeometricRepair(IReadOnlyList<int> values, int index)
        {
            int pair = index <= 1 ? 2 : 0;
            if (values[pair + 1] % values[pair] != 0)
            {
                return null;
            }
            int ratio = values[pair + 1] / values[pair];
            return index == 0 ? (values[1] % ratio == 0 ? values[1] / ratio : null) : values[index - 1] * ratio;
        }
    }
}
