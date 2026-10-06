using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SnpEvolution.Evolution.Contracts
{
    // One row of the first-parts table: a kind of part, its ports and goal in words, and its contracts, one per variant
    // (delay k = 1..4, sequencer k = 2, 3).
    public sealed record FirstPart(string Name, string Ports, string Goal, IReadOnlyList<Contract> Contracts);

    // The ten general arithmetic goals the first library is evolved from. None is specific to Fibonacci, so the default
    // of automatic discovery still holds, and the paper's table of given goals is printed from here (Table).
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
                Enumerable.Range(1, 4).Select(ReferenceParts.DelayContract).ToList()),
            new FirstPart("Fan-out", "count in; count out x2", "both outputs carry n", new[] { FanOut() }),
            new FirstPart("Increment", "count in; count out", "output carries n + 1", new[] { CountToCount("increment", n => n + 1) }),
            new FirstPart("Double", "count in; count out", "output carries 2n", new[] { CountToCount("double", n => 2 * n) }),
            new FirstPart("Add", "count in x2; count out", "output carries n1 + n2 (cases cover pairs up to 6 + 6)", new[] { Add() }),
            new FirstPart("Interval to count", "interval in; count out", "output carries n", new[] { IntervalToCount() }),
            new FirstPart("Count to interval (timer)", "count in; interval out", "two output spikes n steps apart", new[] { CountToInterval() }),
            new FirstPart("Register", "count in; count out", "holds n until started again, then drains it", new[] { Register() }),
            new FirstPart("Zero test", "count in; done-zero, done-nonzero", "the right branch fires, the other never", new[] { ZeroTest() }),
            new FirstPart("Sequencer", "done out xk", "fires its outputs in order, each one step after the previous (k = 2, 3)",
                new[] { Sequencer(2), Sequencer(3) }),
        };

        public static IReadOnlyList<Contract> Contracts { get; } = All.SelectMany(part => part.Contracts).ToList();

        public static Contract Named(string name) =>
            Contracts.FirstOrDefault(contract => contract.Name == name) ?? throw new ArgumentException($"No first-part contract is named '{name}'.", nameof(name));

        // Generous, so a slow but correct part is kept for MAP-Elites to make quicker and smaller.
        public static int LatencyFor(int largestValue) => 3 * largestValue + 4;

        public static string Table()
        {
            var text = new StringBuilder("| Part | Ports besides start and done | Contract |\n|---|---|---|\n");
            foreach (FirstPart part in All)
            {
                text.Append($"| {part.Name} | {part.Ports} | {part.Goal} |\n");
            }
            return text.ToString();
        }

        private static Contract FanOut() => new Contract(
            "fan-out",
            Start(),
            new[] { Done() },
            new[] { Port.In("n", PortKind.Count), Port.Out("a", PortKind.Count), Port.Out("b", PortKind.Count) },
            Values.Select(n => Case(new() { ["n"] = n }, new() { ["a"] = n, ["b"] = n })).ToList(),
            LatencyFor(Larger));

        private static Contract CountToCount(string name, Func<int, int> function) => new Contract(
            name,
            Start(),
            new[] { Done() },
            new[] { Port.In("n", PortKind.Count), Port.Out("out", PortKind.Count) },
            Values.Select(n => Case(new() { ["n"] = n }, new() { ["out"] = function(n) })).ToList(),
            LatencyFor(function(Larger)));

        // Every pair up to 6 + 6, then one with a larger addend on each side.
        private static Contract Add()
        {
            List<(int A, int B)> pairs = Enumerable.Range(0, LargestAddend + 1)
                .SelectMany(a => Enumerable.Range(0, LargestAddend + 1).Select(b => (a, b)))
                .Append((Larger, 5))
                .Append((5, Larger))
                .ToList();
            return new Contract(
                "add",
                Start(),
                new[] { Done() },
                new[] { Port.In("a", PortKind.Count), Port.In("b", PortKind.Count), Port.Out("sum", PortKind.Count) },
                pairs.Select(pair => Case(new() { ["a"] = pair.A, ["b"] = pair.B }, new() { ["sum"] = pair.A + pair.B })).ToList(),
                LatencyFor(Larger + 5));
        }

        // An interval is at least 1, so these cases start from 1.
        private static Contract IntervalToCount() => new Contract(
            "interval to count",
            Start(),
            new[] { Done() },
            new[] { Port.In("n", PortKind.Interval), Port.Out("out", PortKind.Count) },
            Values.Where(n => n >= 1).Select(n => Case(new() { ["n"] = n }, new() { ["out"] = n })).ToList(),
            LatencyFor(Larger));

        private static Contract CountToInterval() => new Contract(
            "count to interval",
            Start(),
            new[] { Done() },
            new[] { Port.In("n", PortKind.Count), Port.Out("out", PortKind.Interval) },
            Values.Where(n => n >= 1).Select(n => Case(new() { ["n"] = n }, new() { ["out"] = n })).ToList(),
            LatencyFor(Larger));

        private static Contract Register() => new Contract(
            "register",
            Start(),
            new[] { Done() },
            new[] { Port.In("n", PortKind.Count), Port.Out("out", PortKind.Count) },
            Values.Select(n => Case(new() { ["n"] = n }, new() { ["out"] = n })).ToList(),
            LatencyFor(Larger));

        private static Contract ZeroTest() => new Contract(
            "zero test",
            Start(),
            new[] { Port.Out("zero", PortKind.Trigger), Port.Out("nonzero", PortKind.Trigger) },
            new[] { Port.In("n", PortKind.Count) },
            Values.Select(n => Case(new() { ["n"] = n }, new(), n == 0 ? "zero" : "nonzero")).ToList(),
            LatencyFor(Larger));

        // Output i fires on the (i + 1)th step after start reaches the part and done on the step after the last, which
        // a latency of exactly k + 1 and outputs on rising steps leave as the only way.
        private static Contract Sequencer(int k) => new Contract(
            $"sequencer {k}",
            Start(),
            new[] { Done() },
            Enumerable.Range(1, k).Select(index => Port.Out($"t{index}", PortKind.Trigger)).ToList(),
            new[] { Case(new(), Enumerable.Range(1, k).ToDictionary(index => $"t{index}", _ => 1)) },
            MaxLatency: k + 1,
            MinLatency: k + 1,
            OrderedTriggers: true);

        private static Port Start() => Port.In("start", PortKind.Trigger);

        private static Port Done() => Port.Out("done", PortKind.Trigger);

        private static ContractCase Case(Dictionary<string, int> inputs, Dictionary<string, int> outputs, string done = "done") => new ContractCase(inputs, outputs, done);
    }
}
