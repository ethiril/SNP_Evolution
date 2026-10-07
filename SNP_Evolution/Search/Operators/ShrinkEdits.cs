using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Model;

namespace SnpEvolution.Search.Operators
{
    // Removes a hidden neuron but keeps the path through it: every neuron that sent to it sends to its targets
    // instead. A relay that only passes spikes on costs a step and a neuron, and the hand reductions of SN P systems
    // in the literature remove many of them this way.
    public sealed class BypassNeuron : IMutation
    {
        public Network Mutate(Network network, Random random)
        {
            List<int> hidden = Enumerable.Range(0, network.Neurons.Count).Where(index => NetworkEdits.IsHidden(network.Neurons[index])).ToList();
            if (hidden.Count == 0)
            {
                return network;
            }
            int bypassed = hidden[random.Next(hidden.Count)];
            int position = bypassed + 1;
            IReadOnlyList<int> onward = network.Neurons[bypassed].Connections;
            Network rewired = new Network(network.Neurons
                .Select((neuron, index) => neuron.Connections.Contains(position)
                    ? neuron.WithConnections(NetworkEdits.ValidConnections(neuron.Connections.Concat(onward), index, network.Neurons.Count))
                    : neuron)
                .ToList());
            return NetworkEdits.RemoveNeuron(rewired, bypassed);
        }
    }

    // Folds one hidden neuron into another that has the same rules: the kept neuron sends to the targets of both,
    // and every neuron that sent to either sends to it. Compiled networks repeat the same small neurons, such as one
    // gate per instruction, and two of them that are never busy at the same time can be one.
    public sealed class MergeNeurons : IMutation
    {
        public Network Mutate(Network network, Random random)
        {
            List<int> hidden = Enumerable.Range(0, network.Neurons.Count).Where(index => NetworkEdits.IsHidden(network.Neurons[index])).ToList();
            if (hidden.Count < 2)
            {
                return network;
            }
            int kept = hidden[random.Next(hidden.Count)];
            List<int> alike = hidden.Where(index => index != kept && SameRules(network.Neurons[index], network.Neurons[kept])).ToList();
            if (alike.Count == 0)
            {
                return network;
            }
            int folded = alike[random.Next(alike.Count)];
            int keptPosition = kept + 1;
            int foldedPosition = folded + 1;
            Network merged = new Network(network.Neurons
                .Select((neuron, index) =>
                {
                    IEnumerable<int> connections = neuron.Connections.Select(target => target == foldedPosition ? keptPosition : target);
                    if (index == kept)
                    {
                        connections = connections.Concat(network.Neurons[folded].Connections.Select(target => target == foldedPosition ? keptPosition : target));
                        neuron = neuron.WithInitialSpikes(neuron.InitialSpikes + network.Neurons[folded].InitialSpikes);
                    }
                    return neuron.WithConnections(NetworkEdits.ValidConnections(connections, index, network.Neurons.Count));
                })
                .ToList());
            return NetworkEdits.RemoveNeuron(merged, folded);
        }

        private static bool SameRules(Neuron first, Neuron second) =>
            first.Rules.Select(rule => rule.Key).SequenceEqual(second.Rules.Select(rule => rule.Key));
    }
}
