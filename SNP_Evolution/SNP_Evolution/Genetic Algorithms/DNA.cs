using System;
using System.Collections.Generic;
using SNP_First_Test.Network;
using SNP_Network = SNP_First_Test.Network.Network;


namespace SNP_First_Test.Genetic_Algorithms
{
    public class DNA
    {
        // Network which is contained in this DNA
        public SNP_Network Genes { get; set; }
        // Fitness of this DNA
        public float Fitness { get; set; }
        // Random()
        private Random random;
        // Get a new randomized network for this DNA
        private Func<SNP_Network> getRandomNetwork;
        // Get a new random rule
        private Func<List<string>, Random, string> getModifiedRule;
        // accepted templates
        private List<string> acceptedRegex;
        // The provided Fitness Function for this DNA
        private Func<int, float> fitnessFunction;

        // Gene is of type SNP_Network
        public DNA(Random random, Func<SNP_Network> getRandomNetwork, Func<List<string>, Random, string> getModifiedRule, List<string> acceptedRegex, Func<int, float> fitnessFunction, bool init = true)
        {
            this.random = random;
            this.getRandomNetwork = getRandomNetwork;
            this.getModifiedRule = getModifiedRule;
            this.acceptedRegex = acceptedRegex;
            this.fitnessFunction = fitnessFunction;
            if (init)
            {
                // start a new random Network
                Genes = getRandomNetwork();
            }
        }

        /// <summary>
        /// Use the fitness function passed through from the main file
        /// </summary>
        /// <param name="index">index of the population that the fitness is being calculated for</param>
        /// <returns>float value of the fitness</returns>
        public float CalculateFitness(int index)
        {
            // calculate the fitness for the current gene
            Fitness = fitnessFunction(index);
            return Fitness;
        }


        /// <summary>
        /// Combine the DNA of two parents into a child
        /// </summary>
        /// <param name="secondParent">Second piece of DNA we will be combining</param>
        /// <returns></returns>
        public DNA Crossover(DNA secondParent)
        {
            // New default child
            DNA child = new DNA(random, getRandomNetwork, getModifiedRule, acceptedRegex, fitnessFunction, init: false);

            // for now crossover with always the same amount of genes and rules 
            int neuronAmount = Genes.Neurons.Count;
            int outputNeuron = 0;
            for (int i = 0; i < neuronAmount; i++)
            {
                if (Genes.Neurons[i].IsOutput)
                {
                    outputNeuron = i;
                }
            }
            List<Neuron> neurons = new List<Neuron>();
            for (int i = 0; i < neuronAmount; i++)
            {
                int ruleAmount = Genes.Neurons[i].Rules.Count;
                List<Rule> rules = new List<Rule>();
                for (int j = 0; j < ruleAmount; j++)
                {
                    // since for this test the delays and Fire rules will remain the same through every network, we only need to generate new rule expressions.
                    string nextExp = random.NextDouble() < 0.5 ? Genes.Neurons[i].Rules[j].RuleExpression : secondParent.Genes.Neurons[i].Rules[j].RuleExpression;
                    rules.Add(new Rule(nextExp, Genes.Neurons[i].Rules[j].Delay, Genes.Neurons[i].Rules[j].Fire));
                }
                List<int> connections = new List<int>();
                int connectionCount = Genes.Neurons[i].Connections.Count;
                // we want to repeat the connections for all of the networks, these aren't changing for now.
                for (int k = 0; k < connectionCount; k++)
                {
                    connections.Add(Genes.Neurons[i].Connections[k]);
                }
                neurons.Add(new Neuron(rules, Genes.Neurons[i].SpikeCount, connections, (i == outputNeuron)));
            }
            child.Genes = new SNP_Network(neurons);
            return child;
        }


        /// <summary>
        /// Random chance (at a given % rate) to mutate a rule during the evolution
        /// </summary>
        /// <param name="mutationRate"></param>
        public void Mutate(float mutationRate)
        {
            if (random.NextDouble() < mutationRate)
            {
                int neuron = random.Next(0, Genes.Neurons.Count);
                int rule = random.Next(0, Genes.Neurons[neuron].Rules.Count);
                // Change one of the rules at random
                Genes.Neurons[neuron].Rules[rule] = new Rule(getModifiedRule(acceptedRegex, random), Genes.Neurons[neuron].Rules[rule].Delay, Genes.Neurons[neuron].Rules[rule].Fire);
            }
        }

    }
}
