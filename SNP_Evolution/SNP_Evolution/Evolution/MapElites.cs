using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution
{
    // MAP-Elites: an archive keeps the best network for every cell, and children of random elites compete only with
    // the elite of their own cell. When the task describes behaviour (how much of a sequence a network gets right and
    // how long a gap it can make, say) cells are behaviours, so stepping stones that are wrong for now but can do
    // something new are kept. Otherwise cells are sizes (neuron count by rule count), so one run maps out how fitness
    // trades against size and small networks are never crowded out by large ones. Parents are picked from the archive
    // at random unless a selection is given, such as lexicase, which favours elites right about different parts.
    public sealed class MapElites : IGeneticAlgorithm
    {
        private const double CrossoverChance = 0.3;

        private readonly int batchSize;
        private readonly Random random;
        private readonly Func<Network> createRandomNetwork;
        private readonly IPopulationEvaluator evaluator;
        private readonly ICrossover crossover;
        private readonly IMutation mutation;
        private readonly IParentSelection? parents;
        private readonly Dictionary<(bool Behaviour, int, int), Individual> archive = new Dictionary<(bool, int, int), Individual>();
        private readonly List<IReadOnlyList<float>> fitnessHistory = new List<IReadOnlyList<float>>();
        private List<Individual> elites = new List<Individual>();
        private List<Network> immigrants = new List<Network>();

        public MapElites(int batchSize, Random random, Func<Network> createRandomNetwork, IPopulationEvaluator evaluator, ICrossover crossover, IMutation mutation,
            IParentSelection? parents = null)
        {
            this.parents = parents;
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

        public static (bool Behaviour, int, int) CellOf(Individual individual)
        {
            if (individual.Niche is (int first, int second))
            {
                return (true, first, second);
            }
            (int neurons, int rules) = Cell(individual.Genes);
            return (false, neurons, rules);
        }

        public void NextGeneration()
        {
            Func<Individual> chooseParent = parents != null && elites.Count > 0 ? parents.Prepare(elites, random) : () => elites[random.Next(elites.Count)];
            List<Individual> batch = immigrants.Take(batchSize)
                .Concat(Enumerable.Range(0, Math.Max(0, batchSize - immigrants.Count)).Select(_ => NewNetwork(chooseParent)))
                .Select(network => new Individual(network))
                .ToList();
            immigrants = new List<Network>();
            Evaluation.Evaluate(evaluator, batch);
            fitnessHistory.Add(batch.Select(individual => individual.Fitness).Where(GeneticAlgorithm.IsRecordableFitness).ToList());
            Archive(batch);
            Generation++;
        }

        // Newcomers make up part of the next batch, and only displace an elite they beat.
        public void Immigrate(IReadOnlyList<Network> newcomers) => immigrants = newcomers.ToList();

        // Every elite is scored again and the archive rebuilt, since both scores and behaviours can change.
        public void Rescore()
        {
            List<Individual> previous = archive.Values.ToList();
            archive.Clear();
            if (previous.Count == 0)
            {
                return;
            }
            Evaluation.Evaluate(evaluator, previous);
            Archive(previous);
        }

        private void Archive(IEnumerable<Individual> candidates)
        {
            foreach (Individual candidate in candidates)
            {
                (bool, int, int) cell = CellOf(candidate);
                if (!archive.TryGetValue(cell, out Individual? incumbent) || Ranking.Compare(candidate, incumbent) <= 0)
                {
                    archive[cell] = candidate;
                }
            }
            elites = Ranking.Rank(archive.Values);
            Best = elites[0];
        }

        private Network NewNetwork(Func<Individual> chooseParent)
        {
            if (elites.Count == 0)
            {
                return createRandomNetwork();
            }
            Network parent = chooseParent().Genes;
            if (elites.Count > 1 && random.NextDouble() < CrossoverChance)
            {
                parent = crossover.Cross(parent, chooseParent().Genes, random);
            }
            return mutation.Mutate(parent, random);
        }
    }
}
