using System.Text.RegularExpressions;
using SnpEvolution.Search.Genome;

namespace SnpEvolution.Tests.Search.Genome
{
    public class ExpressionGeneratorTests
    {
        [Fact]
        public void ExpressionsFillBothPlaceholdersWithShortSpikeRuns()
        {
            var generator = new ExpressionGenerator(new[] { "x(y)+" }, maxSpikeGroupSize: 4, new Random(0));

            for (int sample = 0; sample < 50; sample++)
            {
                Assert.Matches(new Regex(@"^a{1,3}\(a{1,3}\)\+$"), generator.Next());
            }
        }

        [Fact]
        public void EachPlaceholderGetsItsOwnSpikeRun()
        {
            var generator = new ExpressionGenerator(new[] { "x,y" }, maxSpikeGroupSize: 4, new Random(0));

            bool anyDiffer = Enumerable.Range(0, 50).Select(_ => generator.Next().Split(',')).Any(parts => parts[0] != parts[1]);

            Assert.True(anyDiffer);
        }
    }
}
