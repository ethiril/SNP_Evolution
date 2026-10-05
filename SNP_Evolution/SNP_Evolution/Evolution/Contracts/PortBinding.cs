using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Evolution.Contracts
{
    // 1-based neuron positions for out-ports and done ports; in-ports feed the input neurons in contract order instead.
    public sealed record PortBinding(IReadOnlyDictionary<string, int> Positions)
    {
        // Evolution fixes this binding so every network in a run reads its results from the same neurons.
        public static PortBinding AfterInputs(Contract contract)
        {
            int inputs = 1 + contract.DataIn.Count();
            return new PortBinding(OutPorts(contract).Select((port, index) => (port.Name, Position: inputs + 1 + index))
                .ToDictionary(pair => pair.Name, pair => pair.Position));
        }

        public static IEnumerable<Port> OutPorts(Contract contract) => contract.DataOut.Concat(contract.Done);

        public int this[string port] => Positions[port];

        public int NeuronsNeeded => Positions.Values.DefaultIfEmpty(0).Max();

        // Throws ArgumentException unless every out-port and done port has its own neuron.
        public PortBinding ValidatedFor(Contract contract)
        {
            var problems = new List<string>();
            problems.AddRange(OutPorts(contract).Where(port => !Positions.ContainsKey(port.Name)).Select(port => $"Port '{port.Name}' has no neuron."));
            problems.AddRange(Positions.Where(pair => pair.Value < 1).Select(pair => $"Port '{pair.Key}' is bound to position {pair.Value}; positions start at 1."));
            problems.AddRange(Positions.GroupBy(pair => pair.Value).Where(group => group.Count() > 1)
                .Select(group => $"Ports {string.Join(", ", group.Select(pair => $"'{pair.Key}'"))} share neuron {group.Key}; each needs its own."));
            if (problems.Count > 0)
            {
                throw new ArgumentException($"The port binding does not fit contract '{contract.Name}': " + string.Join(" ", problems));
            }
            return this;
        }
    }
}
