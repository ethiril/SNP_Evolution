using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Algorithms
{
    // NEAT-style evolution. Networks are grouped into species of similar structure, each network's fitness is shared
    // with its species, and species breed among themselves. A new structure therefore only competes with its own
    // kind while it improves, instead of being wiped out by a population that is fitter for now. Species that stop
    // improving lose their offspring, and the threshold for "similar" adapts to hold a target number of species.
    public sealed class SpeciatedAlgorithm : IGeneticAlgorithm
    {
        private const double InitialThreshold = 1.5;
        private const int StagnationLimit = 15;
        private const double CrossoverChance = 0.5;
        private const int MinimumSizeForElite = 3;

        private readonly int populationSize;
        private readonly Random random;
        private readonly IPopulationEvaluator evaluator;
        private readonly ICrossover crossover;
        private readonly IMutation mutation;
        private readonly int targetSpecies;
        private readonly ScoreHistory fitnessHistory = new ScoreHistory();
        private List<Species> species = new List<Species>();
        private List<Individual> population;
        private double threshold = InitialThreshold;

        public SpeciatedAlgorithm(int populationSize, Random random, Func<Network> createRandomNetwork, IPopulationEvaluator evaluator, ICrossover crossover, IMutation mutation)
        {
            this.populationSize = populationSize;
            this.random = random;
            this.evaluator = evaluator;
            this.crossover = crossover;
            this.mutation = mutation;
            targetSpecies = Math.Max(2, populationSize / 8);
            population = Enumerable.Range(0, populationSize).Select(_ => new Individual(createRandomNetwork())).ToList();
        }

        public IReadOnlyList<Individual> Population => population;

        public int Generation { get; private set; } = 1;

        public Individual? Best { get; private set; }

        public IReadOnlyList<IReadOnlyList<float>> FitnessHistory => fitnessHistory.Rows;

        public int SpeciesCount => species.Count;

        // How different two networks' structures are: differences in size count fully, and neurons at the same
        // position are compared by their synapses, rules and initial spikes.
        public static double Distance(Network first, Network second)
        {
            double distance = Math.Abs(first.Neurons.Count - second.Neurons.Count) + 0.5 * Math.Abs(first.RuleCount - second.RuleCount);
            int shared = Math.Min(first.Neurons.Count, second.Neurons.Count);
            if (shared == 0)
            {
                return distance;
            }
            double neuronDifference = 0;
            for (int index = 0; index < shared; index++)
            {
                Neuron left = first.Neurons[index];
                Neuron right = second.Neurons[index];
                int union = left.Connections.Union(right.Connections).Count();
                double synapses = union == 0 ? 0 : 1 - (double)left.Connections.Intersect(right.Connections).Count() / union;
                int ruleSlots = Math.Max(left.Rules.Count, right.Rules.Count);
                double rules = (double)Enumerable.Range(0, ruleSlots).Count(slot => !SameRule(left.Rules.ElementAtOrDefault(slot), right.Rules.ElementAtOrDefault(slot))) / ruleSlots;
                double spikes = Math.Min(1, Math.Abs(left.InitialSpikes - right.InitialSpikes) / 3.0);
                neuronDifference += synapses + rules + spikes;
            }
            return distance + neuronDifference / shared;
        }

        public void NextGeneration()
        {
            Evaluation.Evaluate(evaluator, population);
            fitnessHistory.Record(population);
            population = Ranking.Rank(population);
            Best = Best == null || Ranking.IsBetter(population[0], Best) ? population[0] : Best;
            Speciate();
            population = Breed();
            Generation++;
        }

        // The champion is first in the population waiting to be scored, so newcomers replace children from the end.
        public void Immigrate(IReadOnlyList<Network> newcomers) => population = Immigrants.ReplaceWeakest(population, newcomers, 1);

        // The population waiting is scored next generation anyway; the best ever and the species' records are from
        // the old task, so they go.
        public void Rescore()
        {
            Best = null;
            species.Clear();
        }

        private static bool SameRule(Rule? first, Rule? second) =>
            first != null && second != null && first.Key == second.Key;

        private void Speciate()
        {
            foreach (Species existing in species)
            {
                existing.Members.Clear();
            }
            foreach (Individual individual in population)
            {
                Species? home = species.FirstOrDefault(candidate => Distance(candidate.Representative, individual.Genes) < threshold);
                if (home == null)
                {
                    home = new Species(individual.Genes);
                    species.Add(home);
                }
                home.Members.Add(individual);
            }
            species = species.Where(group => group.Members.Count > 0).ToList();
            foreach (Species group in species)
            {
                group.Update(random);
            }
            threshold *= species.Count < targetSpecies ? 0.9 : species.Count > targetSpecies ? 1.1 : 1;
            threshold = Math.Max(0.1, threshold);
        }

        private List<Individual> Breed()
        {
            Individual champion = population[0];
            List<Species> breeding = species
                .Where(group => group.Stagnant < StagnationLimit || group.Members.Contains(champion))
                .ToList();
            int[] allotment = Allot(breeding);
            var next = new List<Individual>();
            for (int index = 0; index < breeding.Count; index++)
            {
                List<Individual> members = Ranking.Rank(breeding[index].Members);
                int count = allotment[index];
                if (count > 0 && members.Count >= MinimumSizeForElite)
                {
                    next.Add(members[0]);
                    count--;
                }
                List<Individual> parents = members.Take(Math.Max(1, (members.Count + 1) / 2)).ToList();
                for (int child = 0; child < count; child++)
                {
                    Network genes = parents[random.Next(parents.Count)].Genes;
                    if (parents.Count > 1 && random.NextDouble() < CrossoverChance)
                    {
                        genes = crossover.Cross(genes, parents[random.Next(parents.Count)].Genes, random);
                    }
                    next.Add(new Individual(mutation.Mutate(genes, random)));
                }
            }
            if (!next.Contains(champion))
            {
                next.Insert(0, champion);
            }
            return next.Take(populationSize).ToList();
        }

        // Shares each species' summed fitness out among its members, then hands out the offspring in proportion,
        // giving any left over from rounding to the largest remainders.
        private int[] Allot(IReadOnlyList<Species> breeding)
        {
            double[] shares = breeding.Select(group => group.Members.Sum(member => Math.Max(0, member.Fitness)) / group.Members.Count + 1e-6).ToArray();
            double total = shares.Sum();
            double[] exact = shares.Select(share => share / total * populationSize).ToArray();
            int[] allotment = exact.Select(value => (int)Math.Floor(value)).ToArray();
            foreach (int index in Enumerable.Range(0, breeding.Count).OrderByDescending(index => exact[index] - allotment[index]).Take(populationSize - allotment.Sum()))
            {
                allotment[index]++;
            }
            return allotment;
        }

        private sealed class Species
        {
            public Species(Network representative)
            {
                Representative = representative;
            }

            public Network Representative { get; private set; }

            public List<Individual> Members { get; } = new List<Individual>();

            public float BestFitness { get; private set; } = float.MinValue;

            // Generations since the species last improved on its best fitness.
            public int Stagnant { get; private set; }

            public void Update(Random random)
            {
                float best = Members.Max(member => member.Fitness);
                Stagnant = best > BestFitness ? 0 : Stagnant + 1;
                BestFitness = Math.Max(BestFitness, best);
                Representative = Members[random.Next(Members.Count)].Genes;
            }
        }
    }
}
