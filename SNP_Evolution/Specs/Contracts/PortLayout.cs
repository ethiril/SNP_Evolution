using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Specs.Contracts
{
    // A contract's port at a 1-based neuron position.
    public sealed record PartPort(Port Port, int Position);

    // Where a contract's ports sit in a network: in-ports on the input neurons in contract order, start first, and
    // out-ports and done ports on the neurons a binding names. Evolved and hand-built parts put the in-ports first and the
    // out-ports straight after them (AfterInputs).
    public static class PortLayout
    {
        public static IEnumerable<Port> InPorts(Contract contract) => new[] { contract.Start }.Concat(contract.DataIn);

        public static IEnumerable<Port> OutPorts(Contract contract) => contract.DataOut.Concat(contract.Done);

        // Evolution fixes this binding so every network in a run reads its results from the same neurons.
        public static PortBinding AfterInputs(Contract contract)
        {
            int inputs = InPorts(contract).Count();
            return new PortBinding(OutPorts(contract).Select((port, index) => (port.Name, Position: inputs + 1 + index))
                .ToDictionary(pair => pair.Name, pair => pair.Position));
        }

        // Every port, in-ports on the given input neuron positions in order.
        public static IReadOnlyList<PartPort> Of(Contract contract, PortBinding binding, IEnumerable<int> inputPositions) =>
            InPorts(contract).Zip(inputPositions, (port, position) => new PartPort(port, position))
                .Concat(OutPorts(contract).Select(port => new PartPort(port, binding[port.Name])))
                .ToList();

        // Every port of a network whose input neurons come first, as a task's networks have them.
        public static IReadOnlyList<PartPort> InputsFirst(Contract contract, PortBinding binding) =>
            Of(contract, binding, Enumerable.Range(1, InPorts(contract).Count()));
    }
}
