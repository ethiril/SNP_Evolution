using System;
using System.Collections.Generic;
using SnpEvolution.Model;

namespace SnpEvolution.Search.Operators
{
    // With probability rate, replaces one randomly chosen rule's expression with a freshly generated one.
    public sealed class RuleExpressionMutation : IMutation
    {
        private readonly float rate;
        private readonly Func<string> createRandomExpression;

        public RuleExpressionMutation(float rate, Func<string> createRandomExpression)
        {
            this.rate = rate;
            this.createRandomExpression = createRandomExpression;
        }

        public Network Mutate(Network network, Random random)
        {
            if (random.NextDouble() >= rate)
            {
                return network;
            }
            int neuronIndex = random.Next(0, network.Neurons.Count);
            IReadOnlyList<Rule> rules = network.Neurons[neuronIndex].Rules;
            int ruleIndex = random.Next(0, rules.Count);
            return network.WithRule(neuronIndex, ruleIndex, rules[ruleIndex].WithExpression(createRandomExpression()));
        }
    }
}
