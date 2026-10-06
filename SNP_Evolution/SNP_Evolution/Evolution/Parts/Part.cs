using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Parts
{
    public sealed record Part(Contract Contract, Network Network, PortBinding Binding)
    {
        public ContractTask Task() => new ContractTask(Contract, Binding);

        // In-ports are the input neurons in contract order, start first, since the binding only places out-ports.
        public IReadOnlyList<PartPort> Ports() =>
            PortLayout.Of(Contract, Binding, Network.Neurons.Select((neuron, index) => (neuron, index)).Where(pair => pair.neuron.IsInput).Select(pair => pair.index + 1));
    }
}
