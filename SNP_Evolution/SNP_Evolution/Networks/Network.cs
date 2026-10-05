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

        public void Print()
        {
            Console.WriteLine("Network breakdown");
            for (int index = 0; index < Neurons.Count; index++)
            {
                Neuron neuron = Neurons[index];
                Console.WriteLine("Neuron: {0}, Initial Spikes: {1}, Rule Amount: {2}, Current Rules: ", index + 1, neuron.InitialSpikes, neuron.Rules.Count);
                Console.Write(string.Concat(neuron.Rules.Select(rule => $"{rule.Expression} -> {rule.Fire};{rule.Delay}, ")));
                Console.Write("Neuron connections: ");
                Console.Write(string.Concat(neuron.Connections.Select(connection => connection + ", ")));
                Console.WriteLine("Is output neuron: " + neuron.IsOutput);
            }
        }
    }
}
