using System;
using System.Collections.Generic;
using System.Linq;
using static SnpEvolution.Evolution.Contracts.Specifications;

namespace SnpEvolution.Evolution.Contracts
{
    // Cases include zero operands and one larger case, where hand-built SN P arithmetic usually breaks. Each contract is a
    // specification and the values its cases test.
    public static class ArithmeticParts
    {
        // Binary operands are 4 bits and a product 8, the scales the published Loihi 2 and FPGA adders report at.
        public const int OperandBits = 4;
        public const int ProductBits = 8;

        public const int LargestFactor = 4;

        public static IReadOnlyList<CatalogueEntry> CountEntries { get; } = new[]
        {
            CatalogueEntry.Of(Subtract(PortKind.Count), Enumerable.Range(0, FirstParts.LargestAddend + 1).SelectMany(a => Enumerable.Range(0, a + 1).Select(b => Pair(a, b)))
                .Append(Pair(FirstParts.Larger, 5))),
            CatalogueEntry.Of(Multiply(PortKind.Count), FactorPairs()),
            CatalogueEntry.Of(Divide(PortKind.Count), Enumerable.Range(0, FirstParts.Largest + 1).SelectMany(a => Enumerable.Range(1, LargestFactor).Select(b => Pair(a, b)))
                .Append(Pair(FirstParts.Larger, 5))),
            CatalogueEntry.Of(Compare(PortKind.Count), Enumerable.Range(0, LargestFactor + 1).SelectMany(a => Enumerable.Range(0, LargestFactor + 1).Select(b => Pair(a, b)))
                .Append(Pair(FirstParts.Larger, 5)).Append(Pair(5, FirstParts.Larger))),
        };

        public static IReadOnlyList<CatalogueEntry> BinaryEntries { get; } = new[]
        {
            CatalogueEntry.Of(Subtract(PortKind.Binary), BinaryPairs().Where(pair => pair[0] >= pair[1])),
            CatalogueEntry.Of(Multiply(PortKind.Binary), BinaryPairs()),
            CatalogueEntry.Of(Divide(PortKind.Binary), BinaryPairs().Where(pair => pair[1] >= 1)),
            CatalogueEntry.Of(Compare(PortKind.Binary), BinaryPairs()),
        };

        public static IReadOnlyList<CatalogueEntry> BuildingBlockEntries { get; } = new[]
        {
            CatalogueEntry.Of(AddConstant(2), FirstParts.Each(FirstParts.Values)),
            CatalogueEntry.Of(Specifications.Decrement, FirstParts.Each(FirstParts.Values.Where(n => n >= 1))),
            CatalogueEntry.Of(Specifications.Gate, FirstParts.Values.SelectMany(n => new[] { 0, 1 }.Select(open => Pair(n, open)))),
            // Its cases put 0 or 2 in a, and every factor pair in b and n.
            CatalogueEntry.Of(Specifications.AddLoop, FactorPairs().SelectMany(pair => new[] { 0, 2 }.Select(a => (IReadOnlyList<int>)new[] { a, pair[0], pair[1] }))),
        };

        // Every catalogued contract with its specification.
        public static IReadOnlyList<CatalogueEntry> KnownEntries { get; } = FirstParts.Entries.Concat(CountEntries).Concat(BinaryEntries).Concat(BuildingBlockEntries).ToList();

        public static IReadOnlyList<Contract> Count { get; } = CountEntries.Select(entry => entry.Contract).ToList();

        public static IReadOnlyList<Contract> Binary { get; } = BinaryEntries.Select(entry => entry.Contract).ToList();

        public static IReadOnlyList<Contract> Contracts { get; } = Count.Concat(Binary).ToList();

        public static IReadOnlyList<Contract> BuildingBlocks { get; } = BuildingBlockEntries.Select(entry => entry.Contract).ToList();

        // Every contract a part file may name, so a saved part cannot quietly change one.
        public static IReadOnlyList<Contract> Known { get; } = KnownEntries.Select(entry => entry.Contract).ToList();

        public static Contract Named(string name) =>
            Known.FirstOrDefault(contract => contract.Name == name) ?? throw new ArgumentException($"No known contract is named '{name}'.", nameof(name));

        public static Contract AddTwo() => BuildingBlocks[0];

        public static Contract Decrement() => BuildingBlocks[1];

        public static Contract Gate() => BuildingBlocks[2];

        public static Contract AddLoop() => BuildingBlocks[3];

        private static IReadOnlyList<int> Pair(int a, int b) => new[] { a, b };

        private static List<IReadOnlyList<int>> FactorPairs() =>
            Enumerable.Range(0, LargestFactor + 1)
                .SelectMany(a => Enumerable.Range(0, LargestFactor + 1).Select(b => Pair(a, b)))
                .Append(Pair(6, 5))
                .ToList();

        // Zero, one, small values and the largest 4-bit value on each side.
        private static List<IReadOnlyList<int>> BinaryPairs()
        {
            int[] values = { 0, 1, 2, 3, 5, 6, 9, 15 };
            return values.SelectMany(a => values.Select(b => Pair(a, b))).ToList();
        }
    }
}
