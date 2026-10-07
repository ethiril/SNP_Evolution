using SnpEvolution.Model;
using SnpEvolution.Search;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Search.Genome;
using SnpEvolution.Search.Operators;
using static SnpEvolution.Tests.Fixtures.GeneticAlgorithms;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Search.Algorithms
{
    public class ImmigrantsTests
    {
        [Fact]
        public void GenerationalImmigrantsReplaceTheWeakestButNotTheElite()
        {
            var random = new Random(4);
            NetworkFactory factory = Factories.StandardRules(random);
            GeneticAlgorithm algorithm = Generational(6, random, factory.NewNetwork, new RecordingEvaluator(network => 1f / network.Neurons.Count), WeightedMutation.Structural(1, factory));
            algorithm.NextGeneration();
            Network elite = algorithm.Population[0].Genes;
            Network[] newcomers = { NeverOutputs(), NeverOutputs(), NeverOutputs() };

            algorithm.Immigrate(newcomers);

            Assert.Equal(6, algorithm.Population.Count);
            Assert.Same(elite, algorithm.Population[0].Genes);
            Assert.Equal(newcomers, algorithm.Population.Skip(3).Select(individual => individual.Genes));
        }

        [Fact]
        public void AsManyImmigrantsAsThePopulationStillLeaveTheEliteInPlace()
        {
            var random = new Random(4);
            NetworkFactory factory = Factories.StandardRules(random);
            var evaluator = new RecordingEvaluator(network => 1f / network.Neurons.Count);
            var algorithms = new IGeneticAlgorithm[]
            {
                Generational(6, random, factory.NewNetwork, evaluator, WeightedMutation.Structural(1, factory)),
                new SpeciatedAlgorithm(6, random, factory.NewNetwork, evaluator, new NeuronCrossover(), WeightedMutation.Structural(1, factory)),
            };
            foreach (IGeneticAlgorithm algorithm in algorithms)
            {
                algorithm.NextGeneration();
                Network elite = algorithm.Population[0].Genes;

                algorithm.Immigrate(Enumerable.Range(0, 6).Select(_ => NeverOutputs()).ToList());

                Assert.Same(elite, algorithm.Population[0].Genes);
                Assert.Equal(6, algorithm.Population.Count);
            }
        }

        [Fact]
        public void NewcomersAreMadeOnlyFromScoredHosts()
        {
            var scored = new Individual(TestNetworks.Identity());
            scored.Record(new FitnessResult(-1, Array.Empty<int>()));
            var unscored = new Individual(NeverOutputs());

            List<Network> made = Immigrants.FromBest(new[] { unscored, scored }, hosts: 1, wanted: 1, attempts: 1, host => host);

            Assert.Same(scored.Genes, Assert.Single(made));
        }

        [Fact]
        public void ArchiveAndStrategyImmigrantsJoinTheNextBatch()
        {
            var random = new Random(5);
            NetworkFactory factory = Factories.StandardRules(random);
            var evaluator = new RecordingEvaluator(network => 1f / network.Neurons.Count);
            var algorithms = new IGeneticAlgorithm[]
            {
                new MapElites(6, random, factory.NewNetwork, evaluator, new NeuronCrossover(), WeightedMutation.Structural(1, factory)),
                new MuPlusLambdaStrategy(2, 6, random, factory.NewNetwork, evaluator, WeightedMutation.Structural(1, factory)),
            };
            foreach (IGeneticAlgorithm algorithm in algorithms)
            {
                algorithm.NextGeneration();
                Network newcomer = NeverOutputs();
                algorithm.Immigrate(new[] { newcomer });
                evaluator.Seen.Clear();

                algorithm.NextGeneration();

                Assert.Contains(newcomer, evaluator.Seen);
                Assert.Equal(6, evaluator.Seen.Count);
            }
        }
    }
}
