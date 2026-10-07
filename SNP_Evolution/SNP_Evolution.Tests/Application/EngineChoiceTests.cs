using SnpEvolution.Application;

namespace SnpEvolution.Tests.Application
{
    public class EngineChoiceTests
    {
        [Fact]
        public void APartsOriginNamesTheSampledEngineButNotTheDefault()
        {
            Assert.Equal(" --engine sampled", new EngineChoice(Sampled: true).CommandLineFlag);
            Assert.Equal("", new EngineChoice().CommandLineFlag);
        }
    }
}
