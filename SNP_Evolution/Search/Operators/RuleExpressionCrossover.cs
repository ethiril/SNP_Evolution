using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Model;

namespace SnpEvolution.Search.Operators
{
    // The child keeps the first parent's topology, taking each rule expression from either parent.
    public sealed class RuleExpressionCrossover : ICrossover
    {
        public Network Cross(Network firstParent, Network secondParent, Random random)
        {
            var neurons = new List<Neuron>();
            for (int neuronIndex = 0; neuronIndex < firstParent.Neurons.Count; neuronIndex++)
            {
                Neuron neuron = firstParent.Neurons[neuronIndex];
                IReadOnlyList<Rule>? secondRules = secondParent.Neurons.ElementAtOrDefault(neuronIndex)?.Rules;
                neurons.Add(neuron.WithRules(neuron.Rules.Select((rule, ruleIndex) =>
                {
                    bool keepFirst = random.NextDouble() < 0.5;
                    Rule? secondRule = secondRules?.ElementAtOrDefault(ruleIndex);
                    return keepFirst || secondRule == null ? rule : rule.WithExpression(secondRule.Expression);
                })));
            }
            return new Network(neurons);
        }
    }
}
