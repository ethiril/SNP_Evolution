using System;
using System.Collections.Generic;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution
{
    public static class RandomTopology
    {
        public static Network Create(ExpressionGenerator expressions, int maxInitialSpikes, Random random)
        {
            int neuronCount = random.Next(2, 8);
            int outputPosition = random.Next(1, neuronCount + 1);
            var neurons = new List<Neuron>();
            for (int index = 0; index < neuronCount; index++)
            {
                List<Rule> rules = CreateRules(expressions, random);
                List<int> connections = CreateConnections(index + 1, neuronCount, random);
                long initialSpikes = random.Next(0, maxInitialSpikes + 1);
                neurons.Add(new Neuron(rules, initialSpikes, connections, index + 1 == outputPosition));
            }
            return new Network(neurons);
        }

        private static List<Rule> CreateRules(ExpressionGenerator expressions, Random random)
        {
            int ruleCount = random.Next(1, 4);
            var rules = new List<Rule>();
            for (int index = 0; index < ruleCount; index++)
            {
                bool fire = random.Next(0, 2) == 1;
                string expression = expressions.Next();
                rules.Add(new Rule(expression, random.Next(0, 2), fire));
            }
            return rules;
        }

        private static List<int> CreateConnections(int ownPosition, int neuronCount, Random random)
        {
            int guaranteedTarget = random.Next(1, neuronCount + 1);
            while (guaranteedTarget == ownPosition)
            {
                guaranteedTarget = random.Next(1, neuronCount + 1);
            }
            var connections = new List<int> { guaranteedTarget };
            for (int position = 2; position <= neuronCount; position++)
            {
                if (position != ownPosition && !connections.Contains(position) && random.Next(0, 2) == 1)
                {
                    connections.Add(position);
                }
            }
            connections.Sort();
            return connections;
        }
    }
}
