using SnpEvolution.Application;
using SnpEvolution.Model;
using SnpEvolution.Search;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Search.Genome;
using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Tests.Application
{
    public class RunLayersTests
    {
        [Fact]
        public void AContractRunJustShortOfAPerfectScoreIsNotSolved()
        {
            var individual = new Individual(TestNetworks.AlwaysOutputsOne());
            individual.Record(new FitnessResult(0.99f, Array.Empty<int>(), "", Exact: true));
            var run = new FixedPopulation(individual);

            Assert.True(RunLayers.IsSolved(run));
            Assert.False(RunLayers.IsSolved(run, new ContractTask(PartFixtures.RegisterContract())));
        }

        private sealed class FixedPopulation(Individual best) : IGeneticAlgorithm
        {
            public IReadOnlyList<Individual> Population => new[] { best };

            public int Generation => 1;

            public Individual? Best => best;

            public IReadOnlyList<IReadOnlyList<float>> FitnessHistory => Array.Empty<IReadOnlyList<float>>();

            public void NextGeneration() { }

            public void Immigrate(IReadOnlyList<Network> newcomers) { }

            public void Rescore() { }
        }
    }
}
