using SnpEvolution.Model;
using SnpEvolution.Search.Genome;
using static SnpEvolution.Tests.Fixtures.WellFormedNetworks;

namespace SnpEvolution.Tests.Search.Genome
{
    public class NetworkFactoryTests
    {
        [Fact]
        public void HardwareProfileIsOffUnlessAskedFor()
        {
            Assert.False(new GenomeSpace().HardwareProfile);
        }

        [Fact]
        public void NetworksMadeUnderTheProfileFitItWithoutBeingConformed()
        {
            var space = new GenomeSpace(InputCount: 2, RuleForm: RuleForm.Mixed, MaxNeurons: 8, MaxDelay: 3, DuplicateNeurons: true, HardwareProfile: true);
            NetworkFactory factory = Factories.Networks(space, new Random(1));

            for (int sample = 0; sample < 300; sample++)
            {
                Network made = factory.NewNetwork();
                Assert.Empty(HardwareProfile.Problems(made));
            }
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
