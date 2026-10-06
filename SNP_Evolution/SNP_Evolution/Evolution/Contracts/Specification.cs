using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Evolution.Contracts
{
    // What a contract means for every input rather than for its test cases: which inputs it covers, the case each one
    // should give, and the latency allowed when every input is at most a bound. A bounded check proves a part against it.
    public sealed record Specification(
        Func<IReadOnlyDictionary<string, int>, bool> InDomain,
        Func<IReadOnlyDictionary<string, int>, ContractCase> Expected,
        Func<int, int> Latency);

    // Contracts are stored as test cases, so a specification is found by the contract's name and checked against its cases
    // before it is trusted: a contract that only shares a known name is not proven against the wrong function.
    public static class Specifications
    {
        // Null when the contract has no specification, as a proposed contract does.
        public static Specification? For(Contract contract)
        {
            if (!contract.DataIn.Any())
            {
                return contract.Cases.Count == 1 ? new Specification(_ => true, _ => contract.Cases[0], _ => contract.MaxLatency) : null;
            }
            bool binary = contract.DataIn.All(port => port.Kind == PortKind.Binary);
            string name = binary ? contract.Name.Replace($" {ArithmeticParts.OperandBits}-bit", "") : contract.Name;
            if (Counted(name) is not Specification counted)
            {
                return null;
            }
            Specification specification = counted with { Expected = inputs => counted.Expected(inputs) with { Inputs = inputs } };
            // A binary port bounds its values, so the contract's own latency already covers every input.
            return binary ? specification with { Latency = _ => contract.MaxLatency } : specification;
        }

        // Each case of the contract the specification leaves out or reads differently; empty when they agree.
        public static IReadOnlyList<string> Disagreements(Contract contract, Specification specification) =>
            contract.Cases.Select((@case, index) => (@case, index))
                .Select(pair => !specification.InDomain(pair.@case.Inputs) ? $"case {pair.index + 1} is outside the specification's domain"
                    : !SameCase(specification.Expected(pair.@case.Inputs), pair.@case) ? $"case {pair.index + 1} expects other outputs than the specification"
                    : null)
                .Where(problem => problem != null)
                .Select(problem => problem!)
                .ToList();

        private static bool SameCase(ContractCase expected, ContractCase given) =>
            expected.Done == given.Done && expected.Outputs.Count == given.Outputs.Count
            && expected.Outputs.All(pair => given.Outputs.TryGetValue(pair.Key, out int value) && value == pair.Value);

        // Latencies grow with the values as the contracts' own were set, from FirstParts.LatencyFor of the largest value
        // in the cases; loops take a round per unit of one operand, each about as long as the other operand.
        private static Specification? Counted(string name)
        {
            switch (name)
            {
                case "fan-out":
                    return Single("n", n => Outputs(("a", n), ("b", n)), FirstParts.LatencyFor);
                case "increment":
                    return Single("n", n => Outputs(("out", n + 1)), bound => FirstParts.LatencyFor(bound + 1));
                case "double":
                    return Single("n", n => Outputs(("out", 2 * n)), bound => FirstParts.LatencyFor(2 * bound));
                case "interval to count":
                case "count to interval":
                    return Single("n", n => Outputs(("out", n)), FirstParts.LatencyFor, n => n >= 1);
                case "register":
                    return Single("n", n => Outputs(("out", n)), FirstParts.LatencyFor);
                case "decrement":
                    return Single("n", n => Outputs(("out", n - 1)), FirstParts.LatencyFor, n => n >= 1);
                case "zero test":
                    return new Specification(_ => true, inputs => Case(Outputs(), inputs["n"] == 0 ? "zero" : "nonzero"), FirstParts.LatencyFor);
                case "add":
                    return Pair("a", "b", (a, b) => Outputs(("sum", a + b)), bound => FirstParts.LatencyFor(2 * bound));
                case "gate":
                    return new Specification(inputs => inputs["open"] <= 1, inputs => Case(Outputs(("out", inputs["n"] * inputs["open"]))), FirstParts.LatencyFor);
                case "add loop":
                    return new Specification(_ => true, inputs => Case(Outputs(("sum", inputs["a"] + inputs["b"] * inputs["n"]))), Loop);
                case "subtract":
                    return Pair("a", "b", (a, b) => Outputs(("difference", a - b)), FirstParts.LatencyFor, (a, b) => a >= b);
                case "multiply":
                    return Pair("a", "b", (a, b) => Outputs(("product", a * b)), Loop);
                case "divide":
                    return Pair("a", "b", (a, b) => Outputs(("quotient", a / b), ("remainder", a % b)), Loop, (_, b) => b >= 1);
                case "compare":
                    return new Specification(_ => true, inputs => Case(Outputs(), inputs["a"] < inputs["b"] ? "less" : "not less"), FirstParts.LatencyFor);
            }
            if (name.StartsWith("add ") && int.TryParse(name[4..], out int k) && k >= 1)
            {
                return Single("n", n => Outputs(("out", n + k)), bound => FirstParts.LatencyFor(k * (bound + k)));
            }
            return null;
        }

        private static int Loop(int bound) => Math.Max(ArithmeticParts.LoopLatency, (bound + 1) * (4 * bound + 40));

        private static Specification Single(string port, Func<int, Dictionary<string, int>> outputs, Func<int, int> latency, Func<int, bool>? inDomain = null) =>
            new Specification(inputs => inDomain?.Invoke(inputs[port]) ?? true, inputs => Case(outputs(inputs[port])), latency);

        private static Specification Pair(string first, string second, Func<int, int, Dictionary<string, int>> outputs, Func<int, int> latency, Func<int, int, bool>? inDomain = null) =>
            new Specification(inputs => inDomain?.Invoke(inputs[first], inputs[second]) ?? true, inputs => Case(outputs(inputs[first], inputs[second])), latency);

        private static Dictionary<string, int> Outputs(params (string Port, int Value)[] values) => values.ToDictionary(value => value.Port, value => value.Value);

        private static ContractCase Case(Dictionary<string, int> outputs, string done = "done") => new ContractCase(new Dictionary<string, int>(), outputs, done);
    }
}
