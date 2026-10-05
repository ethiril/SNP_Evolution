using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Modules
{
    // Neurons cut out of a network with only the synapses among themselves, and the ports where they met the rest:
    // Inputs (indices into Body) received spikes from outside, Outputs sent spikes outside. A neuron that was the
    // network's output stays marked as one, and counts as an output port.
    public sealed record Cut(Network Body, IReadOnlyList<int> Inputs, IReadOnlyList<int> Outputs)
    {
        // Identifies a cut by what it is, so the same part found twice is only kept once.
        public string Key => NetworkNotation.Format(Body) + "in " + string.Join(",", Inputs) + " out " + string.Join(",", Outputs);
    }

    public static class ModuleCuts
    {
        // Cuts out the neurons at the given indices; input neurons are never part of a module.
        public static Cut Cut(Network network, IEnumerable<int> indices)
        {
            List<int> chosen = indices.Distinct().Where(index => !network.Neurons[index].IsInput).OrderBy(index => index).ToList();
            var position = chosen.Select((index, order) => (index, order)).ToDictionary(pair => pair.index + 1, pair => pair.order + 1);
            var body = new Network(chosen
                .Select(index => network.Neurons[index])
                .Select(neuron => neuron
                    .WithConnections(neuron.Connections.Where(position.ContainsKey).Select(target => position[target]))
                    .WithRoles(neuron.IsOutput, false)
                    .WithModule(null))
                .ToList());
            List<int> inputs = chosen.Select((index, order) => (index, order))
                .Where(pair => network.Neurons.Where((_, other) => !position.ContainsKey(other + 1)).Any(neuron => neuron.Connections.Contains(pair.index + 1)))
                .Select(pair => pair.order)
                .ToList();
            List<int> outputs = chosen.Select((index, order) => (index, order))
                .Where(pair => network.Neurons[pair.index].IsOutput || network.Neurons[pair.index].Connections.Any(target => !position.ContainsKey(target)))
                .Select(pair => pair.order)
                .ToList();
            return new Cut(body, inputs, outputs);
        }

        // The whole network as a module, without neurons that cannot affect the output.
        public static Cut Whole(Network network)
        {
            Network pruned = Prune(network);
            return Cut(pruned, Enumerable.Range(0, pruned.Neurons.Count));
        }

        // Removes the neurons with no path to the output neuron. Spikes they receive never reach the output, so the
        // network gives the same output without them.
        public static Network Prune(Network network)
        {
            int output = network.Neurons.ToList().FindIndex(neuron => neuron.IsOutput);
            if (output < 0)
            {
                return network;
            }
            var reaches = new HashSet<int> { output + 1 };
            bool grew = true;
            while (grew)
            {
                grew = false;
                for (int index = 0; index < network.Neurons.Count; index++)
                {
                    if (!reaches.Contains(index + 1) && network.Neurons[index].Connections.Any(reaches.Contains))
                    {
                        grew = reaches.Add(index + 1);
                    }
                }
            }
            for (int index = network.Neurons.Count - 1; index >= 0; index--)
            {
                if (!reaches.Contains(index + 1) && !network.Neurons[index].IsInput)
                {
                    network = NetworkEdits.RemoveNeuron(network, index);
                }
            }
            return network;
        }

        // The child's neurons that are not in the parent: by position when no neuron was added or removed, since
        // then nothing was renumbered, and otherwise by whether the parent has the same neuron anywhere.
        public static IReadOnlyList<int> Changed(Network parent, Network child)
        {
            if (parent.Neurons.Count == child.Neurons.Count)
            {
                return Enumerable.Range(0, child.Neurons.Count).Where(index => Signature(child.Neurons[index]) != Signature(parent.Neurons[index])).ToList();
            }
            var unmatched = parent.Neurons.Select(Signature).GroupBy(signature => signature).ToDictionary(group => group.Key, group => group.Count());
            var changed = new List<int>();
            for (int index = 0; index < child.Neurons.Count; index++)
            {
                string signature = Signature(child.Neurons[index]);
                if (unmatched.TryGetValue(signature, out int count) && count > 0)
                {
                    unmatched[signature] = count - 1;
                }
                else
                {
                    changed.Add(index);
                }
            }
            return changed;
        }

        // The changed neurons and the ones they send to or receive from, at most limit of them, changed ones first;
        // null when the change was too spread out to make one part.
        public static IReadOnlyList<int>? AroundChanges(Network network, IReadOnlyList<int> changed, int limit)
        {
            List<int> core = changed.Where(index => !network.Neurons[index].IsInput).ToList();
            if (core.Count == 0 || core.Count > limit)
            {
                return null;
            }
            var positions = new HashSet<int>(core.Select(index => index + 1));
            IEnumerable<int> neighbours = Enumerable.Range(0, network.Neurons.Count)
                .Where(index => !positions.Contains(index + 1) && !network.Neurons[index].IsInput)
                .Where(index => network.Neurons[index].Connections.Any(positions.Contains) || core.Any(own => network.Neurons[own].Connections.Contains(index + 1)));
            return core.Concat(neighbours).Take(limit).ToList();
        }

        internal static string Signature(Neuron neuron) =>
            $"{RulesText(neuron)}/{neuron.InitialSpikes}/{string.Join(",", neuron.Connections)}/{neuron.IsOutput}";

        internal static string RulesText(Neuron neuron) =>
            string.Join("|", neuron.Rules.Select(rule => $"{rule.Expression}:{rule.Delay}:{rule.Fire}:{rule.Consume}:{rule.Produce}"));
    }
}
