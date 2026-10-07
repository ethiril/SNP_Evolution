using SnpEvolution.Search;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Search.Genome;
using SnpEvolution.Search.Operators;
using static SnpEvolution.Tests.Fixtures.Runs;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Search.Algorithms
{
    public class MapElitesTests
    {
        [Fact]
        public void RescoringTheArchiveUsesTheNewTask()
        {
            var random = new Random(6);
            NetworkFactory factory = Factories.StandardRules(random);
            var evaluator = new RecordingEvaluator(_ => 0.9f);
            var elites = new MapElites(8, random, factory.NewNetwork, evaluator, new NeuronCrossover(), WeightedMutation.Structural(1, factory));
            elites.NextGeneration();
            int cells = elites.Population.Count;

            evaluator.Fitness = _ => 0.2f;
            elites.Rescore();

            Assert.Equal(0.2f, elites.Best!.Fitness);
            Assert.Equal(cells, elites.Population.Count);
            Assert.All(elites.Population, elite => Assert.Equal(0.2f, elite.Fitness));
        }

        [Fact]
        public void BehaviourNichesReplaceSizeCellsWhenTheTaskHasThem()
        {
            var individual = new Individual(Identity());
            individual.Record(new FitnessResult(0.5f, Array.Empty<int>()));
            Assert.Equal((false, 2, 2), MapElites.CellOf(individual));

            individual.Record(new FitnessResult(0.5f, Array.Empty<int>(), Niche: (3, 4)));
            Assert.Equal((true, 3, 4), MapElites.CellOf(individual));
        }

        [Fact]
        public void MapElitesKeepsOneEliteForEachSize()
        {
            var random = new Random(0);
            NetworkFactory factory = Factories.StandardRules(random);
            var elites = new MapElites(10, random, factory.NewNetwork, new RecordingEvaluator(ThreeNeurons), new NeuronCrossover(), WeightedMutation.Structural(1, factory));

            for (int generation = 0; generation < 10; generation++)
            {
                elites.NextGeneration();
            }

            Assert.Equal(elites.Population.Count, elites.Population.Select(elite => MapElites.Cell(elite.Genes)).Distinct().Count());
            Assert.True(elites.Population.Count > 3);
        }
    }
}
