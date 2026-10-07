using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Specs.Contracts
{
    // What a part computes for every input rather than for its test cases: its name and ports, which inputs it covers,
    // the case each input should give, and the latency it is allowed on that input. A contract is built from it and the
    // inputs to test (ContractFor); a bounded check proves a part against it for every input up to a bound, and has
    // checked them all at LargestInput when the domain has a largest value.
    public sealed record Specification(
        string Name,
        IReadOnlyList<Port> Done,
        IReadOnlyList<Port> Data,
        Func<IReadOnlyDictionary<string, int>, bool> InDomain,
        Func<IReadOnlyDictionary<string, int>, ContractCase> Expected,
        Func<IReadOnlyDictionary<string, int>, int> Latency,
        int MinLatency = 0,
        bool OrderedTriggers = false,
        bool TogetherTriggers = false,
        int? LargestInput = null)
    {
        public static Port Start => Port.In("start", PortKind.Trigger);

        public static Port DoneOut => Port.Out("done", PortKind.Trigger);

        public IEnumerable<Port> DataIn => Data.Where(port => port.Direction == PortDirection.In);

        // The contract whose cases are the given inputs, each row a value per data in-port in order, and whose latency is
        // the most any of them is allowed.
        public Contract ContractFor(IEnumerable<IReadOnlyList<int>> values)
        {
            List<IReadOnlyDictionary<string, int>> inputs = values.Select(Inputs).ToList();
            if (inputs.FirstOrDefault(input => !InDomain(input)) is IReadOnlyDictionary<string, int> outside)
            {
                throw new ArgumentException($"The input {string.Join(",", outside.Select(pair => $"{pair.Key}={pair.Value}"))} is outside the domain of {Name}.", nameof(values));
            }
            return new Contract(Name, Start, Done, Data, inputs.Select(Expected).ToList(), inputs.Max(Latency), MinLatency, OrderedTriggers, TogetherTriggers);
        }

        // The one data out-port's value for the given values of the data in-ports, in order, for a task that reads it.
        public int Value(params int[] values) => Expected(Inputs(values)).Outputs.Values.Single();

        // The specification a contract was built from: a catalogued contract's, found by everything the contract says
        // rather than by its name, or for a contract with no data in-ports its own (Fixed). Null otherwise, as for a
        // proposed contract.
        public static Specification? For(Contract contract) =>
            ArithmeticParts.KnownEntries.FirstOrDefault(entry => entry.Contract.SameAs(contract))?.Specification ?? Fixed(contract);

        // A contract with no data in-ports is its own specification: its one case, and its own latency.
        public static Specification? Fixed(Contract contract) =>
            !contract.DataIn.Any() && contract.Cases.Count == 1
                ? new Specification(contract.Name, contract.Done, contract.Data, _ => true, _ => contract.Cases[0], _ => contract.MaxLatency, contract.MinLatency, contract.OrderedTriggers, contract.TogetherTriggers)
                : null;

        private IReadOnlyDictionary<string, int> Inputs(IReadOnlyList<int> values) =>
            DataIn.Zip(values).ToDictionary(pair => pair.First.Name, pair => pair.Second);
    }
}
