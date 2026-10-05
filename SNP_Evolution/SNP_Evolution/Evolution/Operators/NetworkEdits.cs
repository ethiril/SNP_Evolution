using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Operators
{
    // Structural edits that keep a network well formed: synapses stay in range, never loop back to their own
    // neuron, and input and output neurons are never removed.
    public static class NetworkEdits
    {
        public static bool IsHidden(Neuron neuron) => !neuron.IsInput && !neuron.IsOutput;

        public static Network AddNeuron(Network network, Neuron neuron) => new Network(network.Neurons.Append(neuron).ToList());

        // Removes the neuron at the index along with every synapse to it, renumbering the others.
        public static Network RemoveNeuron(Network network, int index)
        {
            int removedPosition = index + 1;
            return new Network(network.Neurons
                .Where((_, position) => position != index)
                .Select(neuron => neuron.WithConnections(neuron.Connections
                    .Where(target => target != removedPosition)
                    .Select(target => target > removedPosition ? target - 1 : target)))
                .ToList());
        }

        public static Network SetConnections(Network network, int index, IEnumerable<int> connections) =>
            network.WithNeuron(index, network.Neurons[index].WithConnections(connections));

        // Drops synapses that point outside the network or back at their own neuron.
        public static IEnumerable<int> ValidConnections(IEnumerable<int> connections, int ownIndex, int neuronCount) =>
            connections.Where(target => target >= 1 && target <= neuronCount && target != ownIndex + 1);

        public static (int Neuron, int Rule) RandomRule(Network network, Random random)
        {
            int neuron = random.Next(network.Neurons.Count);
            return (neuron, random.Next(network.Neurons[neuron].Rules.Count));
        }
    }
}
