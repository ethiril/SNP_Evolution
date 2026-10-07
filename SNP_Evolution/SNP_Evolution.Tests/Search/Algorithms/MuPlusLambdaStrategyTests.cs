using SnpEvolution.Search;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Genome;
using static SnpEvolution.Tests.Fixtures.Runs;

namespace SnpEvolution.Tests.Search.Algorithms
{
    public class MuPlusLambdaStrategyTests
    {
        [Fact]
        public void MuPlusLambdaKeepsMuParents()
        {
            var random = new Random(0);
            NetworkFactory factory = Factories.StandardRules(random);
            var strategy = new MuPlusLambdaStrategy(3, 9, random, factory.NewNetwork, new RecordingEvaluator(ThreeNeurons), WeightedMutation.Structural(1, factory));

            strategy.NextGeneration();
            strategy.NextGeneration();

            Assert.Equal(3, strategy.Population.Count);
        }
    }
}
