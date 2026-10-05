using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Networks
{
    public sealed class Network
    {
        public Network(IReadOnlyList<Neuron> neurons)
        {
            Neurons = neurons;
        }

        public IReadOnlyList<Neuron> Neurons { get; }

        public Network WithRule(int neuronIndex, int ruleIndex, Rule replacement) =>
            new Network(Neurons
                .Select((neuron, index) => index == neuronIndex
                    ? neuron.WithRules(neuron.Rules.Select((rule, position) => position == ruleIndex ? replacement : rule))
                    : neuron)
                .ToList());

        public Network WithRandomExpressions(Func<string> nextExpression) =>
            new Network(Neurons
                .Select(neuron => neuron.WithRules(neuron.Rules.Select(rule => rule.WithExpression(nextExpression()))))
                .ToList());
    }
}
