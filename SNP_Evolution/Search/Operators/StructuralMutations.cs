using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SnpEvolution.Model;
using SnpEvolution.Search.Genome;

namespace SnpEvolution.Search.Operators
{
    // The edits below always change the network when they can, and return it unchanged when they cannot (removing
    // the only rule, say). WeightedMutation decides how often to mutate and which edit to make.

    // Swaps one rule's expression for a freshly generated one: a big jump.
    public sealed class ReplaceExpression : IMutation
    {
        private readonly NetworkFactory factory;

        public ReplaceExpression(NetworkFactory factory) => this.factory = factory;

        public Network Mutate(Network network, Random random)
        {
            (int neuron, int rule) = NetworkEdits.RandomRule(network, random);
            return network.WithRule(neuron, rule, network.Neurons[neuron].Rules[rule].WithExpression(factory.NextExpression()));
        }
    }

    // Lengthens or shortens one run of spikes in an expression, so "aa(aaa)*" can become "aaa(aaa)*": a small step.
    public sealed class NudgeExpression : IMutation
    {
        private static readonly Regex SpikeRun = new Regex("a+", RegexOptions.Compiled);

        public Network Mutate(Network network, Random random)
        {
            (int neuron, int ruleIndex) = NetworkEdits.RandomRule(network, random);
            Rule rule = network.Neurons[neuron].Rules[ruleIndex];
            MatchCollection runs = SpikeRun.Matches(rule.Expression);
            if (runs.Count == 0)
            {
                return network;
            }
            Match run = runs[random.Next(runs.Count)];
            int length = Math.Max(1, run.Length + (random.Next(2) == 0 ? -1 : 1));
            string expression = rule.Expression[..run.Index] + new string('a', length) + rule.Expression[(run.Index + run.Length)..];
            return network.WithRule(neuron, ruleIndex, rule.WithExpression(expression));
        }
    }

    // Changes one of a rule's numbers by one, or flips it between firing and forgetting.
    public sealed class NudgeRule : IMutation
    {
        private readonly GenomeSpace space;

        public NudgeRule(GenomeSpace space) => this.space = space;

        public Network Mutate(Network network, Random random)
        {
            (int neuron, int ruleIndex) = NetworkEdits.RandomRule(network, random);
            Rule rule = network.Neurons[neuron].Rules[ruleIndex];
            int step = random.Next(2) == 0 ? -1 : 1;
            Rule changed = random.Next(rule.IsStandard ? 4 : 2) switch
            {
                0 => rule.WithDelay(Math.Clamp(rule.Delay + step, 0, space.MaxDelay)),
                1 => rule.WithFire(!rule.Fire),
                2 => rule.WithConsume(Math.Max(1, (rule.Consume ?? 1) + step)),
                _ => rule.WithProduce(Math.Clamp(rule.Produce + step, 1, space.MaxProduce)),
            };
            return network.WithRule(neuron, ruleIndex, changed);
        }
    }

    // Switches one rule between the legacy and standard forms, keeping its expression.
    public sealed class SwitchRuleForm : IMutation
    {
        public Network Mutate(Network network, Random random)
        {
            (int neuron, int ruleIndex) = NetworkEdits.RandomRule(network, random);
            Rule rule = network.Neurons[neuron].Rules[ruleIndex];
            return network.WithRule(neuron, ruleIndex, rule.WithConsume(rule.IsStandard ? null : 1));
        }
    }

    public sealed class AddRule : IMutation
    {
        private readonly NetworkFactory factory;

        public AddRule(NetworkFactory factory) => this.factory = factory;

        public Network Mutate(Network network, Random random)
        {
            int index = random.Next(network.Neurons.Count);
            Neuron neuron = network.Neurons[index];
            return neuron.Rules.Count >= factory.Space.MaxRulesPerNeuron || factory.NewRuleBeside(neuron.Rules) is not Rule rule
                ? network
                : network.WithNeuron(index, neuron.WithRules(neuron.Rules.Append(rule)));
        }
    }

    public sealed class RemoveRule : IMutation
    {
        public Network Mutate(Network network, Random random)
        {
            (int index, int rule) = NetworkEdits.RandomRule(network, random);
            Neuron neuron = network.Neurons[index];
            return neuron.Rules.Count <= 1 ? network : network.WithNeuron(index, neuron.WithRules(neuron.Rules.Where((_, position) => position != rule)));
        }
    }

    // Adds or takes away one initial spike; input neurons start empty and stay that way.
    public sealed class NudgeInitialSpikes : IMutation
    {
        public Network Mutate(Network network, Random random)
        {
            int index = random.Next(network.Neurons.Count);
            Neuron neuron = network.Neurons[index];
            return neuron.IsInput
                ? network
                : network.WithNeuron(index, neuron.WithInitialSpikes(Math.Max(0, neuron.InitialSpikes + (random.Next(2) == 0 ? -1 : 1))));
        }
    }

    public sealed class AddSynapse : IMutation
    {
        public Network Mutate(Network network, Random random)
        {
            int count = network.Neurons.Count;
            if (count < 2)
            {
                return network;
            }
            int from = random.Next(count);
            int to = random.Next(count - 1);
            int target = (to >= from ? to + 1 : to) + 1;
            return NetworkEdits.SetConnections(network, from, network.Neurons[from].Connections.Append(target));
        }
    }

    // Removes one synapse, but never a neuron's last one unless it is the output neuron.
    public sealed class RemoveSynapse : IMutation
    {
        public Network Mutate(Network network, Random random)
        {
            int from = random.Next(network.Neurons.Count);
            Neuron neuron = network.Neurons[from];
            int minimum = neuron.IsOutput ? 0 : 1;
            if (neuron.Connections.Count <= minimum)
            {
                return network;
            }
            int removed = neuron.Connections[random.Next(neuron.Connections.Count)];
            return NetworkEdits.SetConnections(network, from, neuron.Connections.Where(target => target != removed));
        }
    }

    // NEAT's add-node: splits a synapse a -> b into a -> new -> b, where the new neuron starts as a plain relay, so the
    // network computes much the same as before and evolution can then specialise the new neuron.
    public sealed class SplitSynapse : IMutation
    {
        private readonly NetworkFactory factory;

        public SplitSynapse(NetworkFactory factory) => this.factory = factory;

        public Network Mutate(Network network, Random random)
        {
            var synapses = network.Neurons.SelectMany((neuron, index) => neuron.Connections.Select(target => (From: index, To: target))).ToList();
            if (network.Neurons.Count >= factory.Space.MaxNeurons || synapses.Count == 0)
            {
                return network;
            }
            (int from, int to) = synapses[random.Next(synapses.Count)];
            int added = network.Neurons.Count + 1;
            Network rewired = NetworkEdits.SetConnections(network, from, network.Neurons[from].Connections.Where(target => target != to).Append(added));
            return NetworkEdits.AddNeuron(rewired, new Neuron(new[] { factory.RelayRule() }, 0, new[] { to }, false));
        }
    }

    // Adds a random neuron wired in from one existing neuron and out to another.
    public sealed class AddNeuron : IMutation
    {
        private readonly NetworkFactory factory;

        public AddNeuron(NetworkFactory factory) => this.factory = factory;

        public Network Mutate(Network network, Random random)
        {
            int count = network.Neurons.Count;
            if (count >= factory.Space.MaxNeurons || count == 0)
            {
                return network;
            }
            int added = count + 1;
            int source = random.Next(count);
            Network wired = NetworkEdits.SetConnections(network, source, network.Neurons[source].Connections.Append(added));
            return NetworkEdits.AddNeuron(wired, factory.NewNeuron(new[] { random.Next(count) + 1 }));
        }
    }

    // Removes a neuron that is neither an input nor the output.
    public sealed class RemoveNeuron : IMutation
    {
        private readonly GenomeSpace space;

        public RemoveNeuron(GenomeSpace space) => this.space = space;

        public Network Mutate(Network network, Random random)
        {
            List<int> hidden = Enumerable.Range(0, network.Neurons.Count).Where(index => NetworkEdits.IsHidden(network.Neurons[index])).ToList();
            return hidden.Count == 0 || network.Neurons.Count <= space.SmallestNetwork
                ? network
                : NetworkEdits.RemoveNeuron(network, hidden[random.Next(hidden.Count)]);
        }
    }

    // Copies a neuron that is not an input, with its rules, spikes and synapses out, and has every neuron that sends
    // to the original send to the copy too. The copy runs in step with the original, so evolution can then reuse a
    // working part, such as a counter, and specialise one of the two.
    public sealed class DuplicateNeuron : IMutation
    {
        private readonly GenomeSpace space;

        public DuplicateNeuron(GenomeSpace space) => this.space = space;

        public Network Mutate(Network network, Random random)
        {
            List<int> candidates = Enumerable.Range(0, network.Neurons.Count).Where(index => !network.Neurons[index].IsInput).ToList();
            if (network.Neurons.Count >= space.MaxNeurons || candidates.Count == 0)
            {
                return network;
            }
            int original = candidates[random.Next(candidates.Count)];
            int originalPosition = original + 1;
            int copyPosition = network.Neurons.Count + 1;
            Neuron source = network.Neurons[original];
            Network fedBoth = new Network(network.Neurons
                .Select(neuron => neuron.Connections.Contains(originalPosition) ? neuron.WithConnections(neuron.Connections.Append(copyPosition)) : neuron)
                .ToList());
            return NetworkEdits.AddNeuron(fedBoth, new Neuron(source.Rules, source.InitialSpikes, source.Connections, false));
        }
    }
}
