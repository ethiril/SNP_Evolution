using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SnpEvolution.Networks;

namespace SnpEvolution.Simulation
{
    // A network flattened into plain arrays, so a step is pure index arithmetic and a GPU backend can upload
    // the arrays unchanged. Variable-length lists use offset arrays: neuron i's rules are RuleStart[i] up to
    // RuleStart[i + 1], and likewise for targets and each rule's accept table.
    public sealed class CompiledNetwork
    {
        private static readonly ConditionalWeakTable<Network, CompiledNetwork> Cache = new ConditionalWeakTable<Network, CompiledNetwork>();

        private readonly long[] initialSpikes;
        private readonly bool[] isOutput;
        private readonly int[] ruleStart;
        private readonly int[] ruleDelay;
        private readonly bool[] ruleFires;
        private readonly long[] ruleConsume;
        private readonly int[] ruleProduce;
        private readonly int[] acceptStart;
        private readonly int[] acceptTail;
        private readonly int[] acceptPeriod;
        private readonly bool[] accepts;
        private readonly int[] targetStart;
        private readonly int[] targets;
        private readonly int[] inputNeurons;

        private CompiledNetwork(Network network)
        {
            IReadOnlyList<Neuron> neurons = network.Neurons;
            List<Rule> rules = neurons.SelectMany(neuron => neuron.Rules).ToList();
            NeuronCount = neurons.Count;
            MaxRulesPerNeuron = neurons.Select(neuron => neuron.Rules.Count).DefaultIfEmpty(0).Max();
            initialSpikes = neurons.Select(neuron => neuron.InitialSpikes).ToArray();
            isOutput = neurons.Select(neuron => neuron.IsOutput).ToArray();
            ruleStart = Offsets(neurons.Select(neuron => neuron.Rules.Count));
            ruleDelay = rules.Select(rule => rule.Delay).ToArray();
            ruleFires = rules.Select(rule => rule.Fire).ToArray();
            ruleConsume = rules.Select(rule => rule.Consume ?? ConsumesAll).ToArray();
            ruleProduce = rules.Select(rule => rule.Fire ? (rule.IsStandard ? rule.Produce : 1) : 0).ToArray();
            acceptStart = Offsets(rules.Select(rule => rule.Condition.Accepts.Length));
            acceptTail = rules.Select(rule => rule.Condition.TailLength).ToArray();
            acceptPeriod = rules.Select(rule => rule.Condition.Period).ToArray();
            accepts = rules.SelectMany(rule => rule.Condition.Accepts.ToArray()).ToArray();
            targetStart = Offsets(neurons.Select(neuron => neuron.Connections.Count));
            targets = neurons.SelectMany(neuron => neuron.Connections.Select(position => position - 1)).ToArray();
            inputNeurons = Enumerable.Range(0, NeuronCount).Where(index => neurons[index].IsInput).ToArray();
        }

        // RuleConsume holds this for a legacy rule, which empties the neuron.
        public const long ConsumesAll = -1;

        public int NeuronCount { get; }

        public int MaxRulesPerNeuron { get; }

        public ReadOnlySpan<long> InitialSpikes => initialSpikes;

        public ReadOnlySpan<bool> IsOutput => isOutput;

        public ReadOnlySpan<int> RuleStart => ruleStart;

        public ReadOnlySpan<int> RuleDelay => ruleDelay;

        public ReadOnlySpan<bool> RuleFires => ruleFires;

        public ReadOnlySpan<long> RuleConsume => ruleConsume;

        // Spikes sent along each synapse when the rule is applied; 0 for a forgetting rule.
        public ReadOnlySpan<int> RuleProduce => ruleProduce;

        public ReadOnlySpan<int> AcceptStart => acceptStart;

        public ReadOnlySpan<int> AcceptTail => acceptTail;

        public ReadOnlySpan<int> AcceptPeriod => acceptPeriod;

        public ReadOnlySpan<bool> Accepts => accepts;

        public ReadOnlySpan<int> TargetStart => targetStart;

        // 0-based, unlike Neuron.Connections.
        public ReadOnlySpan<int> Targets => targets;

        // 0-based indexes of the input neurons, in network order; input k of a task feeds InputNeurons[k].
        public ReadOnlySpan<int> InputNeurons => inputNeurons;

        // Networks are immutable, so each one is compiled once and reused for every run.
        public static CompiledNetwork Of(Network network) => Cache.GetValue(network, created => new CompiledNetwork(created));

        // Mirrors SpikeCondition.Matches over the flattened tables.
        public bool RuleMatches(int rule, long spikes)
        {
            int tail = acceptTail[rule];
            long offset = spikes < tail ? spikes : tail + (spikes - tail) % acceptPeriod[rule];
            return accepts[acceptStart[rule] + (int)offset];
        }

        // A standard rule also needs at least the spikes it consumes.
        public bool RuleApplies(int rule, long spikes) => spikes >= ruleConsume[rule] && RuleMatches(rule, spikes);

        private static int[] Offsets(IEnumerable<int> lengths)
        {
            var offsets = new List<int> { 0 };
            foreach (int length in lengths)
            {
                offsets.Add(offsets[^1] + length);
            }
            return offsets.ToArray();
        }
    }
}
