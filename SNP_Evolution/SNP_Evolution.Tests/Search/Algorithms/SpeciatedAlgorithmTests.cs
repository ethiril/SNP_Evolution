using SnpEvolution.Model;
using SnpEvolution.Search.Algorithms;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Search.Algorithms
{
    public class SpeciatedAlgorithmTests
    {
        [Fact]
        public void SpeciesDistanceIsZeroOnlyForTheSameStructure()
        {
            Assert.Equal(0, SpeciatedAlgorithm.Distance(Identity(), Identity()));
            Assert.True(SpeciatedAlgorithm.Distance(Identity(), ReferenceNetworks.NaturalNumbers()) > 2);
            Assert.Equal(SpeciatedAlgorithm.Distance(Identity(), AlwaysOutputsOne()), SpeciatedAlgorithm.Distance(AlwaysOutputsOne(), Identity()));
        }
    }
}
