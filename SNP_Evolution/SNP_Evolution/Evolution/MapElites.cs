using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution
{
    // MAP-Elites: an archive keeps the best network for every size (neuron count by rule count). Children of random
    // elites compete only with the elite of their own size, so one run maps out how fitness trades against size and
    // small networks are never crowded out by large ones.
    public sealed class MapElites : IGeneticAlgorithm
    {
        private const double CrossoverChance = 0.3;

        private readonly int batchSize;
        private readonly Random random;
        private readonly Func<Network> createRandomNetwork;
        private readonly IPopulationEvaluator evaluator;
        private readonly ICrossover crossover;
        private readonly IMutation mutation;
        private readonly Dictionary<(int Neurons, int Rules), Individual> archive = new Dictionary<(int, int), Individual>();
        private readonly List<IReadOnlyList<float>> fitnessHistory = new List<IReadOnlyList<float>>();
        private List<Individual> elites = new List<Individual>();

        public MapElites(int batchSize, Random random, Func<Network> createRandomNetwork, IPopulationEvaluator evaluator, ICrossover crossover, IMutation mutation)
        {
            this.batchSize = batchSize;
            this.random = random;
            this.createRandomNetwork = createRandomNetwork;
            this.evaluator = evaluator;
            this.crossover = crossover;
            this.mutation = mutation;
        }

        // The archive, fittest first.
        public IReadOnlyList<Individual> Population => elites;

        public int Generation { get; private set; } = 1;

        public Individual? Best { get; private set; }

        public IReadOnlyList<IReadOnlyList<float>> FitnessHistory => fitnessHistory;

        public static (int Neurons, int Rules) Cell(Network network) => (network.Neurons.Count, network.RuleCount);

        public void NextGeneration()
        {
            List<Individual> batch = Enumerable.Range(0, batchSize).Select(_ => new Individual(NewNetwork())).ToList();
            Evaluation.Evaluate(evaluator, batch);
            fitnessHistory.Add(batch.Select(individual => individual.Fitness).Where(GeneticAlgorithm.IsRecordableFitness).ToList());
            foreach (Individual candidate in batch)
            {
                (int, int) cell = Cell(candidate.Genes);
                if (!archive.TryGetValue(cell, out Individual? incumbent) || Ranking.Compare(candidate, incumbent) <= 0)
                {
                    archive[cell] = candidate;
                }
            }
            elites = Ranking.Rank(archive.Values);
            Best = elites[0];
            Generation++;
        }

        private Network NewNetwork()
        {
            if (elites.Count == 0)
            {
                return createRandomNetwork();
            }
            Network parent = elites[random.Next(elites.Count)].Genes;
            if (elites.Count > 1 && random.NextDouble() < CrossoverChance)
            {
                parent = crossover.Cross(parent, elites[random.Next(elites.Count)].Genes, random);
            }
            return mutation.Mutate(parent, random);
        }
    }
}
