using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution
{
    public sealed class GeneticAlgorithm
    {
        private readonly Random random;
        private readonly Func<Network> createRandomNetwork;
        private readonly Func<string> createRandomExpression;
        private readonly Func<Network, FitnessResult> evaluate;
        private readonly int elitism;
        private readonly float mutationRate;

        public GeneticAlgorithm(
            int populationSize,
            Random random,
            Func<Network> createRandomNetwork,
            Func<string> createRandomExpression,
            Func<Network, FitnessResult> evaluate,
            int elitism,
            float mutationRate)
        {
            this.random = random;
            this.createRandomNetwork = createRandomNetwork;
            this.createRandomExpression = createRandomExpression;
            this.evaluate = evaluate;
            this.elitism = elitism;
            this.mutationRate = mutationRate;
            Population = Enumerable.Range(0, populationSize).Select(_ => new Individual(createRandomNetwork())).ToList();
        }

        public List<Individual> Population { get; private set; }

        public int Generation { get; private set; } = 1;

        public Individual? Best { get; private set; }

        // One row per generation after the first, holding every in-range fitness that generation scored.
        public List<List<float>> FitnessHistory { get; } = new List<List<float>>();

        public static bool IsRecordableFitness(float fitness) => fitness >= 0 && fitness <= 1;

        private static bool IsViableFitness(float fitness) => fitness > 0 && fitness <= 1;

        public void NextGeneration()
        {
            if (Population.Count == 0)
            {
                return;
            }
            if (Generation == 1)
            {
                EvaluateUntilViable();
            }
            else
            {
                Population.ForEach(individual => individual.Evaluate(evaluate));
                FitnessHistory.Add(Population.Select(individual => individual.Fitness).Where(IsRecordableFitness).ToList());
            }
            Population.Sort((first, second) => second.Fitness.CompareTo(first.Fitness));
            Best = Population.FirstOrDefault(individual => IsRecordableFitness(individual.Fitness)) ?? Population[0];
            Population = Breed();
            Generation++;
        }

        private void EvaluateUntilViable()
        {
            Population.ForEach(individual => individual.Evaluate(evaluate));
            List<int> nonViable = NonViableIndexes();
            while (nonViable.Count > 0)
            {
                Console.WriteLine("Replacing {0} erroneous networks in the initial population.", nonViable.Count);
                foreach (int index in nonViable)
                {
                    var replacement = new Individual(createRandomNetwork());
                    replacement.Evaluate(evaluate);
                    Population[index] = replacement;
                }
                nonViable = NonViableIndexes();
            }
        }

        private List<int> NonViableIndexes() =>
            Enumerable.Range(0, Population.Count).Where(index => !IsViableFitness(Population[index].Fitness)).ToList();

        private List<Individual> Breed()
        {
            float fitnessSum = Population.Sum(individual => individual.Fitness);
            List<Individual> nextPopulation = Population.Take(elitism).ToList();
            while (nextPopulation.Count < Population.Count)
            {
                Network child = Crossover(ChooseParent(fitnessSum).Genes, ChooseParent(fitnessSum).Genes);
                nextPopulation.Add(new Individual(Mutate(child)));
            }
            return nextPopulation;
        }

        // Roulette-wheel selection, falling back to the top tenth when rounding leaves the wheel unspent.
        private Individual ChooseParent(float fitnessSum)
        {
            double remaining = random.NextDouble() * fitnessSum;
            foreach (Individual individual in Population)
            {
                if (remaining < individual.Fitness)
                {
                    return individual;
                }
                remaining -= individual.Fitness;
            }
            return Population[random.Next(0, Population.Count / 10 + 1)];
        }

        // The child keeps the first parent's topology, taking each rule expression from either parent.
        private Network Crossover(Network firstParent, Network secondParent)
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

        private Network Mutate(Network network)
        {
            if (random.NextDouble() >= mutationRate)
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
