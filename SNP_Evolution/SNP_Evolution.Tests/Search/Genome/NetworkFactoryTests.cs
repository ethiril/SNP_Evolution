using SnpEvolution.Search;
using SnpEvolution.Search.Genome;
using static SnpEvolution.Tests.Fixtures.WellFormedNetworks;

namespace SnpEvolution.Tests.Search.Genome
{
    public class NetworkFactoryTests
    {
        [Fact]
        public void IsOffUnlessAskedFor()
        {
            Assert.False(new GenomeSpace().HardwareProfile);
            Assert.False(new PartSearchSettings(1, 1, 1, SearchCatalog.Evolution[0], () => null!).HardwareProfile);
        }

        [Fact]
        public void RandomNetworksAreWellFormed()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                NetworkFactory factory = SmallFactory(seed, inputCount: seed % 3);
                AssertWellFormed(factory.NewNetwork(), factory.Space);
            }
        }

        [Fact]
        public void StandardSpaceOnlyMakesStandardRules()
        {
            NetworkFactory factory = SmallFactory(1, form: RuleForm.Standard);

            Assert.All(Enumerable.Range(0, 50).Select(_ => factory.NewRule()), rule => Assert.True(rule.IsStandard));
        }
    }
}
