using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SnpEvolution.Model;

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
        internal readonly Rule[] rules;
        internal readonly int[] ruleDelay;
        internal readonly bool[] ruleFires;
        internal readonly long[] ruleConsume;
        internal readonly long[] ruleLeastHeld;
        internal readonly int[] ruleProduce;
        internal readonly DelayKind[] ruleDelayKind;
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
            ruleLeastHeld = new long[ruleCount];
            ruleProduce = new int[ruleCount];
            ruleDelayKind = new DelayKind[ruleCount];
            rules = new Rule[ruleCount];
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
                    rules[rule] = source;
                    ruleDelay[rule] = source.Delay;
                    ruleFires[rule] = source.Fire;
                    ruleConsume[rule] = source.Consume ?? ConsumesAll;
                    ruleLeastHeld[rule] = source.LeastHeld;
                    ruleProduce[rule] = source.Sends;
                    ruleDelayKind[rule] = source.DelayKind;
                    if (source.DelayKind == DelayKind.Axonal)
                    {
                        MaxAxonalDelay = Math.Max(MaxAxonalDelay, source.Delay);
                    }
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

        // The consume uploaded to the GPU for a legacy rule, which empties the neuron.
        public const long ConsumesAll = -1;

        public int NeuronCount { get; }

        public int MaxRulesPerNeuron { get; }

        // The longest axonal delay, which is how many steps ahead a neuron's spikes may be in flight; 0 when no rule has one.
        public int MaxAxonalDelay { get; }

        public ReadOnlySpan<long> InitialSpikes => initialSpikes;

        public ReadOnlySpan<bool> IsOutput => isOutput;

        public ReadOnlySpan<int> RuleStart => ruleStart;

        public ReadOnlySpan<int> RuleDelay => ruleDelay;

        public ReadOnlySpan<bool> RuleFires => ruleFires;

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

        // The source rule at a flat index, for its semantics.
        public Rule Rule(int rule) => rules[rule];

        // Rule.Applies over the flattened tables, which the step reads faster than it follows each rule's objects.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool RuleApplies(int rule, long spikes) =>
            spikes >= ruleLeastHeld[rule] && accepts[acceptStart[rule] + SpikeCondition.IndexOf(spikes, acceptTail[rule], acceptPeriod[rule])];
    }
}
