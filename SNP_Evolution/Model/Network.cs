using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace SnpEvolution.Model
{
    public sealed class Network
    {
        public Network(IReadOnlyList<Neuron> neurons)
        {
            Neurons = neurons;
        }

        public IReadOnlyList<Neuron> Neurons { get; }

        [JsonIgnore]
        public int RuleCount => Neurons.Sum(neuron => neuron.Rules.Count);

        [JsonIgnore]
        public int SynapseCount => Neurons.Sum(neuron => neuron.Connections.Count);

        // Smaller is better: the measure the field compares hand-built systems by, with neurons weighing the most.
        [JsonIgnore]
        public int Size => 100 * Neurons.Count + 10 * RuleCount + SynapseCount;

        public Network WithRule(int neuronIndex, int ruleIndex, Rule replacement) =>
            new Network(Neurons
                .Select((neuron, index) => index == neuronIndex
                    ? neuron.WithRules(neuron.Rules.Select((rule, position) => position == ruleIndex ? replacement : rule))
                    : neuron)
                .ToList());

        public Network WithNeuron(int neuronIndex, Neuron replacement) =>
            new Network(Neurons.Select((neuron, index) => index == neuronIndex ? replacement : neuron).ToList());

        public Network WithRandomExpressions(Func<string> nextExpression) =>
            new Network(Neurons
                .Select(neuron => neuron.WithRules(neuron.Rules.Select(rule => rule.WithExpression(nextExpression()))))
                .ToList());
    }
}
