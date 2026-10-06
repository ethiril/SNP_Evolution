using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Evolution.Contracts
{
    // The arithmetic the library should compound towards: n1 - n2, n1 x n2, n1 div n2 with remainder and n1 < n2, each
    // in count encoding and in binary, plus the building blocks hand-built machines are made from. Cases include zero
    // operands and one larger case, where hand-built SN P arithmetic usually breaks.
    public static class ArithmeticParts
    {
        // Binary operands are 4 bits and a product 8, the scales the published Loihi 2 and FPGA adders report at.
        public const int OperandBits = 4;
        public const int ProductBits = 8;

        public const int LargestFactor = 4;

        // Generous for loops: a hand-built add loop spends about 4b + 30 steps on each of its n rounds.
        public const int LoopLatency = 400;

        public static IReadOnlyList<Contract> Count { get; } = new[]
        {
            Subtract(PortKind.Count),
            Multiply(PortKind.Count),
            Divide(PortKind.Count),
            Compare(PortKind.Count),
        };

        public static IReadOnlyList<Contract> Binary { get; } = new[]
        {
            Subtract(PortKind.Binary),
            Multiply(PortKind.Binary),
            Divide(PortKind.Binary),
            Compare(PortKind.Binary),
        };

        public static IReadOnlyList<Contract> Contracts { get; } = Count.Concat(Binary).ToList();

        // Parts machines are built from that are not arithmetic goals themselves.
        public static IReadOnlyList<Contract> BuildingBlocks { get; } = new[] { AddTwo(), Decrement(), Gate(), AddLoop() };

        // Every contract a part file may name, so a saved part cannot quietly change one.
        public static IReadOnlyList<Contract> Known { get; } = FirstParts.Contracts.Concat(Contracts).Concat(BuildingBlocks).ToList();

        public static Contract Named(string name) =>
            Known.FirstOrDefault(contract => contract.Name == name) ?? throw new ArgumentException($"No known contract is named '{name}'.", nameof(name));

        // n -> n + 2, which two chained increments make.
        public static Contract AddTwo() => AddConstant(2);

        // n -> n + k; k chained increments make it, each draining the whole count again.
        public static Contract AddConstant(int k) => new Contract(
            $"add {k}",
            Start(),
            new[] { Done() },
            new[] { Port.In("n", PortKind.Count), Port.Out("out", PortKind.Count) },
            FirstParts.Values.Select(n => Case(new() { ["n"] = n }, new() { ["out"] = n + k })).ToList(),
            FirstParts.LatencyFor(k * (FirstParts.Larger + k)));

        // n -> n - 1 for n of at least 1.
        public static Contract Decrement() => new Contract(
            "decrement",
            Start(),
            new[] { Done() },
            new[] { Port.In("n", PortKind.Count), Port.Out("out", PortKind.Count) },
            FirstParts.Values.Where(n => n >= 1).Select(n => Case(new() { ["n"] = n }, new() { ["out"] = n - 1 })).ToList(),
            FirstParts.LatencyFor(FirstParts.Larger));

        // Passes n on when open is 1 and swallows it when open is 0, the one way a machine can drop a count it no longer needs.
        public static Contract Gate() => new Contract(
            "gate",
            Start(),
            new[] { Done() },
            new[] { Port.In("n", PortKind.Count), Port.In("open", PortKind.Count), Port.Out("out", PortKind.Count) },
            FirstParts.Values.SelectMany(n => new[] { 0, 1 }.Select(open => Case(new() { ["n"] = n, ["open"] = open }, new() { ["out"] = n * open }))).ToList(),
            FirstParts.LatencyFor(FirstParts.Larger));

        // a + n x b: b added to a, n times. Multiplication is this loop with nothing in a.
        public static Contract AddLoop() => new Contract(
            "add loop",
            Start(),
            new[] { Done() },
            new[] { Port.In("a", PortKind.Count), Port.In("b", PortKind.Count), Port.In("n", PortKind.Count), Port.Out("sum", PortKind.Count) },
            FactorPairs().SelectMany(pair => new[] { 0, 2 }.Select(a => Case(new() { ["a"] = a, ["b"] = pair.A, ["n"] = pair.B }, new() { ["sum"] = a + pair.A * pair.B })))
                .ToList(),
            LoopLatency);

        // Every pair up to 4 x 4, then one larger pair.
        private static List<(int A, int B)> FactorPairs() =>
            Enumerable.Range(0, LargestFactor + 1)
                .SelectMany(a => Enumerable.Range(0, LargestFactor + 1).Select(b => (a, b)))
                .Append((6, 5))
                .ToList();

        private static Contract Subtract(PortKind kind)
        {
            List<(int A, int B)> pairs = kind == PortKind.Count
                ? Enumerable.Range(0, FirstParts.LargestAddend + 1).SelectMany(a => Enumerable.Range(0, a + 1).Select(b => (a, b))).Append((FirstParts.Larger, 5)).ToList()
                : BinaryPairs().Where(pair => pair.A >= pair.B).ToList();
            return new Contract(
                Name("subtract", kind),
                Start(),
                new[] { Done() },
                new[] { In("a", kind), In("b", kind), Out("difference", kind, OperandBits) },
                pairs.Select(pair => Case(new() { ["a"] = pair.A, ["b"] = pair.B }, new() { ["difference"] = pair.A - pair.B })).ToList(),
                Latency(kind, FirstParts.Larger));
        }

        private static Contract Multiply(PortKind kind)
        {
            List<(int A, int B)> pairs = kind == PortKind.Count ? FactorPairs() : BinaryPairs();
            return new Contract(
                Name("multiply", kind),
                Start(),
                new[] { Done() },
                new[] { In("a", kind), In("b", kind), Out("product", kind, ProductBits) },
                pairs.Select(pair => Case(new() { ["a"] = pair.A, ["b"] = pair.B }, new() { ["product"] = pair.A * pair.B })).ToList(),
                kind == PortKind.Count ? LoopLatency : Latency(kind, 0));
        }

        // A divisor is at least 1.
        private static Contract Divide(PortKind kind)
        {
            List<(int A, int B)> pairs = kind == PortKind.Count
                ? Enumerable.Range(0, FirstParts.Largest + 1).SelectMany(a => Enumerable.Range(1, LargestFactor).Select(b => (a, b))).Append((FirstParts.Larger, 5)).ToList()
                : BinaryPairs().Where(pair => pair.B >= 1).ToList();
            return new Contract(
                Name("divide", kind),
                Start(),
                new[] { Done() },
                new[] { In("a", kind), In("b", kind), Out("quotient", kind, OperandBits), Out("remainder", kind, OperandBits) },
                pairs.Select(pair => Case(new() { ["a"] = pair.A, ["b"] = pair.B }, new() { ["quotient"] = pair.A / pair.B, ["remainder"] = pair.A % pair.B })).ToList(),
                kind == PortKind.Count ? LoopLatency : Latency(kind, 0));
        }

        // Two done branches, as the zero test has, rather than a data out-port.
        private static Contract Compare(PortKind kind)
        {
            List<(int A, int B)> pairs = kind == PortKind.Count
                ? Enumerable.Range(0, LargestFactor + 1).SelectMany(a => Enumerable.Range(0, LargestFactor + 1).Select(b => (a, b))).Append((FirstParts.Larger, 5)).Append((5, FirstParts.Larger)).ToList()
                : BinaryPairs();
            return new Contract(
                Name("compare", kind),
                Start(),
                new[] { Port.Out("less", PortKind.Trigger), Port.Out("not less", PortKind.Trigger) },
                new[] { In("a", kind), In("b", kind) },
                pairs.Select(pair => Case(new() { ["a"] = pair.A, ["b"] = pair.B }, new(), pair.A < pair.B ? "less" : "not less")).ToList(),
                Latency(kind, FirstParts.Larger));
        }

        // Zero, one, small values and the largest 4-bit value on each side.
        private static List<(int A, int B)> BinaryPairs()
        {
            int[] values = { 0, 1, 2, 3, 5, 6, 9, 15 };
            return values.SelectMany(a => values.Select(b => (a, b))).ToList();
        }

        private static string Name(string operation, PortKind kind) => kind == PortKind.Count ? operation : $"{operation} {OperandBits}-bit";

        private static int Latency(PortKind kind, int largestValue) => kind == PortKind.Count ? FirstParts.LatencyFor(largestValue) : FirstParts.LatencyFor(2 * OperandBits);

        private static Port In(string name, PortKind kind) => Port.In(name, kind, kind == PortKind.Binary ? OperandBits : 0);

        private static Port Out(string name, PortKind kind, int bits) => Port.Out(name, kind, kind == PortKind.Binary ? bits : 0);

        private static Port Start() => Port.In("start", PortKind.Trigger);

        private static Port Done() => Port.Out("done", PortKind.Trigger);

        private static ContractCase Case(Dictionary<string, int> inputs, Dictionary<string, int> outputs, string done = "done") => new ContractCase(inputs, outputs, done);
    }
}
