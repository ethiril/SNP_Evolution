using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution
{
    // Which rule form random rules take.
    public enum RuleForm
    {
        // a -> a rules that empty the neuron, as the original program evolved.
        Legacy,

        // E/a^c -> a^p;d rules, as in the SN P literature.
        Standard,

        // Either, chosen per rule.
        Mixed,
    }

    // The bounds evolution searches within. The first InputCount neurons of every network are its input neurons.
    // DuplicateNeurons lets mutation copy working neurons, which helps build repeated parts such as counters but
    // disrupts small networks, so it is off unless asked for. HardwareProfile keeps every network within HardwareProfile:
    // one threshold rule per neuron and no initial spikes, whatever the rule form and limits say.
    public sealed record GenomeSpace(
        int InputCount = 0,
        RuleForm RuleForm = RuleForm.Legacy,
        int MinNeurons = 2,
        int MaxNeurons = 7,
        int MaxRulesPerNeuron = 3,
        int MaxDelay = 1,
        int MaxInitialSpikes = 4,
        int MaxProduce = 2,
        bool DuplicateNeurons = false,
        bool HardwareProfile = false)
    {
        public int SmallestNetwork => Math.Max(MinNeurons, InputCount + 1);
    }

    // Creates random rules, neurons and networks within a genome space.
    public sealed class NetworkFactory
    {
        private const double ExactConditionChance = 0.5;
        private const double FiringRuleChance = 0.8;
        private const int SmallestAcceptedSearchLimit = 64;

        private readonly ExpressionGenerator expressions;
        private readonly Random random;

        public NetworkFactory(GenomeSpace space, ExpressionGenerator expressions, Random random)
        {
            Space = space;
            this.expressions = expressions;
            this.random = random;
        }

        public GenomeSpace Space { get; }

        public Random Random => random;

        public string NextExpression() => expressions.Next();

        // The same rules and randomness within other bounds.
        public NetworkFactory WithSpace(GenomeSpace space) => new NetworkFactory(space, expressions, random);

        public Rule NewRule()
        {
            if (Space.HardwareProfile)
            {
                // The threshold follows the fewest spikes a random expression accepts, as the other forms' conditions do.
                int threshold = (int)Math.Max(1, SmallestAccepted(expressions.Next()));
                return HardwareProfile.ThresholdRule(threshold, random.NextDouble() < FiringRuleChance, random.Next(0, Space.MaxDelay + 1));
            }
            bool standard = Space.RuleForm == RuleForm.Standard || (Space.RuleForm == RuleForm.Mixed && random.Next(2) == 0);
            int delay = random.Next(0, Space.MaxDelay + 1);
            if (!standard)
            {
                return new Rule(expressions.Next(), delay, random.Next(0, 2) == 1);
            }
            bool fire = random.NextDouble() < FiringRuleChance;
            int produce = random.Next(1, Space.MaxProduce + 1);
            if (random.NextDouble() < ExactConditionChance)
            {
                string exact = expressions.Next();
                long consumed = Math.Max(1, SmallestAccepted(exact));
                return new Rule(new string('a', (int)consumed), delay, fire, consumed, produce);
            }
            string expression = expressions.Next();
            long smallest = Math.Max(1, SmallestAccepted(expression));
            return new Rule(expression, delay, fire, random.Next(1, (int)smallest + 1), produce);
        }

        // A relay that passes each spike straight on, in the space's rule form.
        public Rule RelayRule() => Space.HardwareProfile ? HardwareProfile.ThresholdRule(1)
            : Space.RuleForm == RuleForm.Legacy ? new Rule("a", 0, true) : new Rule("a", 0, true, 1, 1);

        public Neuron NewNeuron(IReadOnlyList<int> connections, bool isOutput = false, bool isInput = false) =>
            new Neuron(
                Enumerable.Range(0, Space.HardwareProfile ? 1 : random.Next(1, Space.MaxRulesPerNeuron + 1)).Select(_ => NewRule()).ToList(),
                isInput || Space.HardwareProfile ? 0 : random.Next(0, Space.MaxInitialSpikes + 1),
                connections,
                isOutput,
                isInput);

        // Inputs come first, the output is one of the other neurons, and every neuron sends to at least one other.
        public Network NewNetwork()
        {
            int neuronCount = random.Next(Space.SmallestNetwork, Math.Max(Space.SmallestNetwork, Space.MaxNeurons) + 1);
            int output = random.Next(Space.InputCount, neuronCount);
            return new Network(Enumerable.Range(0, neuronCount)
                .Select(index => NewNeuron(RandomConnections(index, neuronCount), index == output, index < Space.InputCount))
                .ToList());
        }

        public IReadOnlyList<int> RandomConnections(int ownIndex, int neuronCount)
        {
            var connections = new List<int>();
            if (neuronCount < 2)
            {
                return connections;
            }
            int guaranteed = random.Next(neuronCount - 1);
            connections.Add((guaranteed >= ownIndex ? guaranteed + 1 : guaranteed) + 1);
            for (int index = 0; index < neuronCount; index++)
            {
                if (index != ownIndex && random.Next(0, 3) == 0)
                {
                    connections.Add(index + 1);
                }
            }
            return connections.Distinct().OrderBy(position => position).ToList();
        }

        // The smallest positive spike count the expression accepts, or 1 when it accepts none.
        private static long SmallestAccepted(string expression)
        {
            SpikeCondition condition = SpikeCondition.Parse(expression);
            for (long count = 1; count < SmallestAcceptedSearchLimit; count++)
            {
                if (condition.Matches(count))
                {
                    return count;
                }
            }
            return 1;
        }
    }
}
