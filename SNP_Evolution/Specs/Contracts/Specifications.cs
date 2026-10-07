using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Specs.Contracts
{
    // Every function a catalogued part computes, written once. FirstParts and ArithmeticParts build their contracts from
    // these and the inputs to test; tasks that compute the same functions read them from here too.
    //
    // A unary part's time grows with its values, so most latencies follow LatencyFor of the largest value the part reads
    // or writes. Loops take a round per unit of one operand, each about as long as the other (Loop). A binary port bounds
    // its values, so a binary part is allowed one latency for every input.
    public static class Specifications
    {
        // Generous for loops: a hand-built add loop spends about 4b + 30 steps on each of its n rounds.
        public const int LoopLatency = 400;

        // Generous, so a slow but correct part is kept for MAP-Elites to make quicker and smaller.
        public static int LatencyFor(int largestValue) => 3 * largestValue + 4;

        public static Specification Delay(int k) =>
            new Specification($"delay {k}", new[] { Specification.DoneOut }, new Port[0], _ => true, Case(_ => Outputs()), _ => k, MinLatency: k);

        // Output i fires on the (i + 1)th step after start reaches the part and done on the step after the last, which
        // a latency of exactly k + 1 and outputs on rising steps leave as the only way.
        public static Specification Sequencer(int k) => new Specification(
            $"sequencer {k}",
            new[] { Specification.DoneOut },
            Enumerable.Range(1, k).Select(index => Port.Out($"t{index}", PortKind.Trigger)).ToList(),
            _ => true,
            Case(_ => Enumerable.Range(1, k).ToDictionary(index => $"t{index}", _ => 1)),
            _ => k + 1,
            MinLatency: k + 1,
            OrderedTriggers: true);

        public static Specification FanOut { get; } =
            Count("fan-out", new[] { CountIn("n"), CountOut("a"), CountOut("b") }, n => Outputs(("a", n["n"]), ("b", n["n"])), n => LatencyFor(n["n"]));

        public static Specification Increment { get; } = CountToCount("increment", n => n + 1);

        public static Specification Double { get; } = CountToCount("double", n => 2 * n);

        public static Specification Register { get; } = CountToCount("register", n => n);

        public static Specification Decrement { get; } = CountToCount("decrement", n => n - 1, n => n >= 1, n => LatencyFor(n));

        public static Specification AddConstant(int k) => CountToCount($"add {k}", n => n + k, latency: n => LatencyFor(k * (n + k)));

        public static Specification Add { get; } =
            Count("add", new[] { CountIn("a"), CountIn("b"), CountOut("sum") }, v => Outputs(("sum", v["a"] + v["b"])), v => LatencyFor(v["a"] + v["b"]));

        // An interval is at least 1.
        public static Specification IntervalToCount { get; } = Count("interval to count", new[] { Port.In("n", PortKind.Interval), CountOut("out") },
            v => Outputs(("out", v["n"])), v => LatencyFor(v["n"]), v => v["n"] >= 1);

        public static Specification CountToInterval { get; } = Count("count to interval", new[] { CountIn("n"), Port.Out("out", PortKind.Interval) },
            v => Outputs(("out", v["n"])), v => LatencyFor(v["n"]), v => v["n"] >= 1);

        public static Specification ZeroTest { get; } = new Specification(
            "zero test",
            new[] { Port.Out("zero", PortKind.Trigger), Port.Out("nonzero", PortKind.Trigger) },
            new[] { CountIn("n") },
            _ => true,
            Case(_ => Outputs(), v => v["n"] == 0 ? "zero" : "nonzero"),
            v => LatencyFor(v["n"]));

        // Passes n on when open is 1 and swallows it when open is 0, the one way a machine can drop a count it no longer needs.
        public static Specification Gate { get; } = Count("gate", new[] { CountIn("n"), CountIn("open"), CountOut("out") },
            v => Outputs(("out", v["n"] * v["open"])), v => LatencyFor(v["n"]), v => v["open"] <= 1);

        // Multiplication is this loop with nothing in a: n rounds of adding b.
        public static Specification AddLoop { get; } = Count("add loop", new[] { CountIn("a"), CountIn("b"), CountIn("n"), CountOut("sum") },
            v => Outputs(("sum", v["a"] + v["b"] * v["n"])), v => Loop(rounds: v["n"], length: v["b"]));

        public static Specification Subtract(PortKind kind) => Arithmetic("subtract", kind, new[] { ("difference", ArithmeticParts.OperandBits) },
            v => Outputs(("difference", v["a"] - v["b"])), v => LatencyFor(v["a"]), v => v["a"] >= v["b"]);

        // b rounds of adding a.
        public static Specification Multiply(PortKind kind) => Arithmetic("multiply", kind, new[] { ("product", ArithmeticParts.ProductBits) },
            v => Outputs(("product", v["a"] * v["b"])), v => Loop(rounds: v["b"], length: v["a"]));

        // A divisor is at least 1. Division is allowed the loop's latency at its divisor, which over a bound's inputs is
        // the most any loop with operands that large is allowed.
        public static Specification Divide(PortKind kind) => Arithmetic("divide", kind, new[] { ("quotient", ArithmeticParts.OperandBits), ("remainder", ArithmeticParts.OperandBits) },
            v => Outputs(("quotient", v["a"] / v["b"]), ("remainder", v["a"] % v["b"])), v => Loop(rounds: v["b"], length: v["b"]), v => v["b"] >= 1);

        // Two done branches, as the zero test has, rather than a data out-port.
        public static Specification Compare(PortKind kind) => new Specification(
            Name("compare", kind),
            new[] { Port.Out("less", PortKind.Trigger), Port.Out("not less", PortKind.Trigger) },
            new[] { In("a", kind), In("b", kind) },
            _ => true,
            Case(_ => Outputs(), v => v["a"] < v["b"] ? "less" : "not less"),
            kind == PortKind.Binary ? _ => BinaryLatency : v => LatencyFor(Math.Max(v["a"], v["b"])));

        // A binary word is OperandBits steps whatever its value, so every input is allowed what two words' values are.
        private static int BinaryLatency => LatencyFor(2 * ArithmeticParts.OperandBits);

        private static int Loop(int rounds, int length) => Math.Max(LoopLatency, (rounds + 1) * (4 * length + 40));

        private static Specification CountToCount(string name, Func<int, int> function, Func<int, bool>? inDomain = null, Func<int, int>? latency = null) =>
            Count(name, new[] { CountIn("n"), CountOut("out") }, v => Outputs(("out", function(v["n"]))),
                v => (latency ?? (n => LatencyFor(function(n))))(v["n"]), inDomain == null ? null : v => inDomain(v["n"]));

        private static Specification Count(string name, Port[] data, Func<IReadOnlyDictionary<string, int>, Dictionary<string, int>> outputs,
            Func<IReadOnlyDictionary<string, int>, int> latency, Func<IReadOnlyDictionary<string, int>, bool>? inDomain = null) =>
            new Specification(name, new[] { Specification.DoneOut }, data, inDomain ?? (_ => true), Case(outputs), latency);

        private static Specification Arithmetic(string operation, PortKind kind, (string Name, int Bits)[] outPorts,
            Func<IReadOnlyDictionary<string, int>, Dictionary<string, int>> outputs, Func<IReadOnlyDictionary<string, int>, int> countLatency,
            Func<IReadOnlyDictionary<string, int>, bool>? inDomain = null) =>
            new Specification(
                Name(operation, kind),
                new[] { Specification.DoneOut },
                new[] { In("a", kind), In("b", kind) }.Concat(outPorts.Select(port => Port.Out(port.Name, kind, kind == PortKind.Binary ? port.Bits : 0))).ToList(),
                inDomain ?? (_ => true),
                Case(outputs),
                kind == PortKind.Binary ? _ => BinaryLatency : countLatency);

        private static string Name(string operation, PortKind kind) => kind == PortKind.Count ? operation : $"{operation} {ArithmeticParts.OperandBits}-bit";

        private static Port In(string name, PortKind kind) => Port.In(name, kind, kind == PortKind.Binary ? ArithmeticParts.OperandBits : 0);

        private static Port CountIn(string name) => Port.In(name, PortKind.Count);

        private static Port CountOut(string name) => Port.Out(name, PortKind.Count);

        private static Func<IReadOnlyDictionary<string, int>, ContractCase> Case(
            Func<IReadOnlyDictionary<string, int>, Dictionary<string, int>> outputs, Func<IReadOnlyDictionary<string, int>, string>? done = null) =>
            inputs => new ContractCase(inputs, outputs(inputs), done?.Invoke(inputs) ?? "done");

        private static Dictionary<string, int> Outputs(params (string Port, int Value)[] values) => values.ToDictionary(value => value.Port, value => value.Value);
    }
}
