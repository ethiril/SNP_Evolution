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

        // Internal so the simulation's step loop indexes the arrays directly.
        internal readonly long[] initialSpikes;
        internal readonly bool[] isOutput;
        internal readonly int[] ruleStart;
        internal readonly int[] ruleDelay;
        internal readonly bool[] ruleFires;
        internal readonly long[] ruleConsume;
        internal readonly int[] ruleProduce;
        internal readonly int[] acceptStart;
        internal readonly int[] acceptTail;
        internal readonly int[] acceptPeriod;
        internal readonly bool[] accepts;
        internal readonly int[] targetStart;
        internal readonly int[] targets;
        internal readonly int[] inputNeurons;

        private CompiledNetwork(Network network)
        {
            IReadOnlyList<Neuron> neurons = network.Neurons;
            NeuronCount = neurons.Count;
            int ruleCount = 0;
            int targetCount = 0;
            foreach (Neuron neuron in neurons)
            {
                ruleCount += neuron.Rules.Count;
                targetCount += neuron.Connections.Count;
                MaxRulesPerNeuron = Math.Max(MaxRulesPerNeuron, neuron.Rules.Count);
            }
            initialSpikes = new long[NeuronCount];
            isOutput = new bool[NeuronCount];
            ruleStart = new int[NeuronCount + 1];
            targetStart = new int[NeuronCount + 1];
            targets = new int[targetCount];
            ruleDelay = new int[ruleCount];
            ruleFires = new bool[ruleCount];
            ruleConsume = new long[ruleCount];
            ruleProduce = new int[ruleCount];
            acceptStart = new int[ruleCount];
            acceptTail = new int[ruleCount];
            acceptPeriod = new int[ruleCount];
            var inputs = new List<int>();
            // Rules with the same condition share one accept table; conditions are cached per expression.
            var tableStart = new Dictionary<SpikeCondition, int>(ReferenceEqualityComparer.Instance);
            var acceptTables = new List<bool>();
            int rule = 0;
            int target = 0;
            for (int index = 0; index < NeuronCount; index++)
            {
                Neuron neuron = neurons[index];
                initialSpikes[index] = neuron.InitialSpikes;
                isOutput[index] = neuron.IsOutput;
                if (neuron.IsInput)
                {
                    inputs.Add(index);
                }
                ruleStart[index] = rule;
                foreach (Rule source in neuron.Rules)
                {
                    SpikeCondition condition = source.Condition;
                    if (!tableStart.TryGetValue(condition, out int start))
                    {
                        start = acceptTables.Count;
                        tableStart[condition] = start;
                        acceptTables.AddRange(condition.Accepts);
                    }
                    ruleDelay[rule] = source.Delay;
                    ruleFires[rule] = source.Fire;
                    ruleConsume[rule] = source.Consume ?? ConsumesAll;
                    ruleProduce[rule] = source.Fire ? (source.IsStandard ? source.Produce : 1) : 0;
                    acceptStart[rule] = start;
                    acceptTail[rule] = condition.TailLength;
                    acceptPeriod[rule] = condition.Period;
                    rule++;
                }
                targetStart[index] = target;
                foreach (int position in neuron.Connections)
                {
                    targets[target++] = position - 1;
                }
            }
            ruleStart[NeuronCount] = rule;
            targetStart[NeuronCount] = target;
            accepts = acceptTables.ToArray();
            inputNeurons = inputs.ToArray();
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RuleMatches(int rule, long spikes)
        {
            int tail = acceptTail[rule];
            long offset = spikes < tail ? spikes : tail + (spikes - tail) % acceptPeriod[rule];
            return accepts[acceptStart[rule] + (int)offset];
        }

        // A standard rule also needs at least the spikes it consumes.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RuleApplies(int rule, long spikes) => spikes >= ruleConsume[rule] && RuleMatches(rule, spikes);
    }
}
