using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static SnpEvolution.Evolution.Contracts.Specifications;

namespace SnpEvolution.Evolution.Contracts
{
    // One row of the first-parts table: a kind of part, its ports and goal in words, and its contracts, one per variant
    // (delay k = 1..4, sequencer k = 2, 3).
    public sealed record FirstPart(string Name, string Ports, string Goal, IReadOnlyList<CatalogueEntry> Entries)
    {
        public IReadOnlyList<Contract> Contracts => Entries.Select(entry => entry.Contract).ToList();
    }

    // The ten general arithmetic goals the first library is evolved from. None is specific to Fibonacci, so the default
    // of automatic discovery still holds, and the paper's table of given goals is printed from here (Table). Each contract
    // is a specification and the values its cases test.
    public static class FirstParts
    {
        // Small values a part could memorise, and one larger value to catch a part that did.
        public const int Largest = 8;
        public const int Larger = 12;

        public const int LargestAddend = 6;

        public static IReadOnlyList<int> Values { get; } = Enumerable.Range(0, Largest + 1).Append(Larger).ToList();

        public static IReadOnlyList<FirstPart> All { get; } = new[]
        {
            new FirstPart("Delay k", "none", "done fires k steps after start (k = 1..4, one contract each)",
                Enumerable.Range(1, 4).Select(k => CatalogueEntry.OneCase(Delay(k))).ToList()),
            new FirstPart("Fan-out", "count in; count out x2", "both outputs carry n", new[] { CatalogueEntry.Of(FanOut, Each(Values)) }),
            new FirstPart("Increment", "count in; count out", "output carries n + 1", new[] { CatalogueEntry.Of(Increment, Each(Values)) }),
            new FirstPart("Double", "count in; count out", "output carries 2n", new[] { CatalogueEntry.Of(Specifications.Double, Each(Values)) }),
            new FirstPart("Add", "count in x2; count out", "output carries n1 + n2 (cases cover pairs up to 6 + 6)", new[] { CatalogueEntry.Of(Add, AddPairs()) }),
            new FirstPart("Interval to count", "interval in; count out", "output carries n", new[] { CatalogueEntry.Of(IntervalToCount, Each(Values.Where(n => n >= 1))) }),
            new FirstPart("Count to interval (timer)", "count in; interval out", "two output spikes n steps apart",
                new[] { CatalogueEntry.Of(CountToInterval, Each(Values.Where(n => n >= 1))) }),
            new FirstPart("Register", "count in; count out", "holds n until started again, then drains it", new[] { CatalogueEntry.Of(Register, Each(Values)) }),
            new FirstPart("Zero test", "count in; done-zero, done-nonzero", "the right branch fires, the other never", new[] { CatalogueEntry.Of(ZeroTest, Each(Values)) }),
            new FirstPart("Sequencer", "done out xk", "fires its outputs in order, each one step after the previous (k = 2, 3)",
                new[] { CatalogueEntry.OneCase(Sequencer(2)), CatalogueEntry.OneCase(Sequencer(3)) }),
        };

        public static IReadOnlyList<CatalogueEntry> Entries { get; } = All.SelectMany(part => part.Entries).ToList();

        public static IReadOnlyList<Contract> Contracts { get; } = Entries.Select(entry => entry.Contract).ToList();

        public static Contract Named(string name) =>
            Contracts.FirstOrDefault(contract => contract.Name == name) ?? throw new ArgumentException($"No first-part contract is named '{name}'.", nameof(name));

        public static string Table()
        {
            var text = new StringBuilder("| Part | Ports besides start and done | Contract |\n|---|---|---|\n");
            foreach (FirstPart part in All)
            {
                text.Append($"| {part.Name} | {part.Ports} | {part.Goal} |\n");
            }
            return text.ToString();
        }

        // One value per row, for a part with one data in-port.
        public static IEnumerable<IReadOnlyList<int>> Each(IEnumerable<int> values) => values.Select(value => new[] { value });

        // Every pair up to 6 + 6, then one with a larger addend on each side.
        private static IEnumerable<IReadOnlyList<int>> AddPairs() =>
            Enumerable.Range(0, LargestAddend + 1)
                .SelectMany(a => Enumerable.Range(0, LargestAddend + 1).Select(b => new[] { a, b }))
                .Append(new[] { Larger, 5 })
                .Append(new[] { 5, Larger });
    }
}
