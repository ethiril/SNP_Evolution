using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution
{
    // The rules integrate-and-fire hardware runs exactly, a^{>=k} / a^* -> a firing or forgetting, so a network within them exports to NIR.
    public static class HardwareProfile
    {
        private const int ListedCounts = 5;

        // Each way the network leaves the profile, naming the neuron and rule; empty when it fits.
        public static IReadOnlyList<string> Problems(Network network)
        {
            var problems = new List<string>();
            for (int index = 0; index < network.Neurons.Count; index++)
            {
                Neuron neuron = network.Neurons[index];
                string name = $"Neuron {index + 1}";
                if (neuron.InitialSpikes != 0)
                {
                    problems.Add($"{name} starts with {neuron.InitialSpikes} spike(s); NIR has no initial state, so profile neurons start empty.");
                }
                if (neuron.Rules.Count > 1)
                {
                    problems.Add($"{name} has {neuron.Rules.Count} rules; a profile neuron has one threshold, so one rule at most.");
                }
                for (int position = 0; position < neuron.Rules.Count; position++)
                {
                    problems.AddRange(RuleProblems(neuron.Rules[position]).Select(problem => $"{name}, rule {position + 1} ({NetworkNotation.Rule(neuron.Rules[position])}): {problem}."));
                }
            }
            return problems;
        }

        public static bool Fits(Network network) => Problems(network).Count == 0;

        // The k of a condition that accepts exactly the counts from k up, k >= 1; null for any other condition.
        public static int? Threshold(SpikeCondition condition)
        {
            if (condition.SmallestAccepted(1) is not long first)
            {
                return null;
            }
            return Enumerable.Range(0, (int)Math.Max(first, condition.TailLength) + condition.Period).All(count => condition.Matches(count) == count >= first) ? (int)first : null;
        }

        // a^{>=k} / a^* -> a, or forgetting; only a firing rule keeps a delay, and its delay is axonal.
        public static Rule ThresholdRule(int threshold, bool fire = true, int delay = 0) =>
            new Rule(Expression(threshold), fire ? delay : 0, fire, axonal: fire && delay > 0);

        public static string Expression(int threshold) => threshold == 1 ? "a+" : $"a{{{threshold},}}";

        // The nearest profile rule: the threshold is the fewest spikes the rule could apply to, and fire and delay are kept.
        public static Rule Conform(Rule rule)
        {
            if (!RuleProblems(rule).Any())
            {
                return rule;
            }
            int threshold = Threshold(rule.Condition) ?? SmallestAccepted(rule);
            return ThresholdRule(threshold, rule.Fire, rule.Delay);
        }

        // Keeps the first rule only, and empties the neuron.
        public static Neuron Conform(Neuron neuron)
        {
            List<Rule> rules = neuron.Rules.Take(1).Select(Conform).ToList();
            bool same = neuron.InitialSpikes == 0 && rules.Count == neuron.Rules.Count && rules.Zip(neuron.Rules).All(pair => ReferenceEquals(pair.First, pair.Second));
            return same ? neuron : neuron.WithRules(rules).WithInitialSpikes(0);
        }

        // The same network when it already fits, so operators can tell nothing changed.
        public static Network Conform(Network network)
        {
            List<Neuron> neurons = network.Neurons.Select(Conform).ToList();
            return neurons.Zip(network.Neurons).All(pair => ReferenceEquals(pair.First, pair.Second)) ? network : new Network(neurons);
        }

        private static IEnumerable<string> RuleProblems(Rule rule)
        {
            if (rule.Consume is long consume)
            {
                yield return $"it consumes {consume} spike(s) rather than all of them";
            }
            if (rule.IsStandard && rule.Fire && rule.Produce != 1)
            {
                yield return $"it sends {rule.Produce} spikes rather than one";
            }
            if (Threshold(rule.Condition) == null)
            {
                yield return $"its condition accepts {AcceptedCounts(rule.Condition)} rather than every count from a threshold up";
            }
            if (rule.Delay > 0 && !rule.Axonal)
            {
                yield return $"its delay of {rule.Delay} {(rule.IsStandard ? "closes" : "holds")} the neuron, and the profile only has axonal delays";
            }
        }

        // "1, 3, 5, ..." or "no count at all".
        private static string AcceptedCounts(SpikeCondition condition)
        {
            List<long> accepted = new List<long>();
            for (long count = 0; accepted.Count <= ListedCounts && count < condition.TailLength + (ListedCounts + 1L) * condition.Period; count++)
            {
                if (condition.Matches(count))
                {
                    accepted.Add(count);
                }
            }
            return accepted.Count == 0 ? "no count at all"
                : string.Join(", ", accepted.Take(ListedCounts)) + (accepted.Count > ListedCounts ? ", ..." : " only");
        }

        // The fewest spikes, at least one, the rule could apply to, or 1 when it applies to none.
        private static int SmallestAccepted(Rule rule) => (int)Math.Min(int.MaxValue, rule.Condition.SmallestAccepted(Math.Max(1, rule.Consume ?? 1)) ?? 1);
    }

    // Puts what another operator makes back within the profile, so every child of a profile run fits it.
    public sealed class ProfileMutation : IMutation
    {
        private readonly IMutation inner;

        public ProfileMutation(IMutation inner) => this.inner = inner;

        public Network Mutate(Network network, Random random) => HardwareProfile.Conform(inner.Mutate(network, random));
    }

    public sealed class ProfileCrossover : ICrossover
    {
        private readonly ICrossover inner;

        public ProfileCrossover(ICrossover inner) => this.inner = inner;

        public Network Cross(Network firstParent, Network secondParent, Random random) => HardwareProfile.Conform(inner.Cross(firstParent, secondParent, random));
    }
}
