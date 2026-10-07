using SnpEvolution.Model;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Fitness;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Search.Algorithms
{
    public class RankingTests
    {
        [Fact]
        public void RankingPrefersTheSmallerOfEquallyFitNetworks()
        {
            var small = new Individual(Identity());
            var large = new Individual(ReferenceNetworks.NaturalNumbers());
            small.Record(new FitnessResult(0.5f, Array.Empty<int>()));
            large.Record(new FitnessResult(0.5f, Array.Empty<int>()));

            Assert.Same(small, Ranking.Rank(new[] { large, small })[0]);

            large.Record(new FitnessResult(0.6f, Array.Empty<int>()));
            Assert.Same(large, Ranking.Rank(new[] { small, large })[0]);
        }
    }
}
