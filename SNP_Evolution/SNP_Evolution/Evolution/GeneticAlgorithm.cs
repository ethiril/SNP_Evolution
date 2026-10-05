using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution
{
    // A generational algorithm: rank everyone, carry the elite over, and breed the rest with the given operators.
    public sealed class GeneticAlgorithm : IGeneticAlgorithm
    {
        private readonly Random random;
        private readonly Func<Network> createRandomNetwork;
        private readonly IPopulationEvaluator evaluator;
        private readonly GeneticOperators operators;
        private readonly int elitism;
        private readonly List<IReadOnlyList<float>> fitnessHistory = new List<IReadOnlyList<float>>();
        private List<Individual> population;

        public GeneticAlgorithm(
            int populationSize,
            Random random,
            Func<Network> createRandomNetwork,
            IPopulationEvaluator evaluator,
            GeneticOperators operators,
            int elitism)
        {
            this.random = random;
            this.createRandomNetwork = createRandomNetwork;
            this.evaluator = evaluator;
            this.operators = operators;
            this.elitism = elitism;
            population = Enumerable.Range(0, populationSize).Select(_ => new Individual(createRandomNetwork())).ToList();
        }

        public IReadOnlyList<Individual> Population => population;

        public int Generation { get; private set; } = 1;

        public Individual? Best { get; private set; }

        // One row per generation after the first, holding every in-range fitness that generation scored.
        public IReadOnlyList<IReadOnlyList<float>> FitnessHistory => fitnessHistory;

        public static bool IsRecordableFitness(float fitness) => fitness >= 0 && fitness <= 1;

        private static bool IsViableFitness(float fitness) => fitness > 0 && fitness <= 1;

        public void NextGeneration()
        {
            if (population.Count == 0)
            {
                return;
            }
            if (Generation == 1)
            {
                EvaluateUntilViable();
            }
            else
            {
                Evaluate(population);
                fitnessHistory.Add(population.Select(individual => individual.Fitness).Where(IsRecordableFitness).ToList());
            }
            population.Sort((first, second) => second.Fitness.CompareTo(first.Fitness));
            Best = population.FirstOrDefault(individual => IsRecordableFitness(individual.Fitness)) ?? population[0];
            population = Breed();
            Generation++;
        }

        private void Evaluate(IReadOnlyList<Individual> individuals)
        {
            IReadOnlyList<FitnessResult> results = evaluator.EvaluateAll(individuals.Select(individual => individual.Genes).ToList());
            for (int index = 0; index < individuals.Count; index++)
            {
                individuals[index].Record(results[index]);
            }
        }

        private void EvaluateUntilViable()
        {
            Evaluate(population);
            List<int> nonViable = NonViableIndexes();
            while (nonViable.Count > 0)
            {
                Console.WriteLine("Replacing {0} erroneous networks in the initial population.", nonViable.Count);
                List<Individual> replacements = nonViable.Select(_ => new Individual(createRandomNetwork())).ToList();
                Evaluate(replacements);
                for (int replacement = 0; replacement < nonViable.Count; replacement++)
                {
                    population[nonViable[replacement]] = replacements[replacement];
                }
                nonViable = NonViableIndexes();
            }
        }

        private List<int> NonViableIndexes() =>
            Enumerable.Range(0, population.Count).Where(index => !IsViableFitness(population[index].Fitness)).ToList();

        private List<Individual> Breed()
        {
            Func<Individual> chooseParent = operators.Selection.Prepare(population, random);
            List<Individual> nextPopulation = population.Take(elitism).ToList();
            while (nextPopulation.Count < population.Count)
            {
                Network child = operators.Crossover.Cross(chooseParent().Genes, chooseParent().Genes, random);
                nextPopulation.Add(new Individual(operators.Mutation.Mutate(child, random)));
            }
            return nextPopulation;
        }
    }
}
