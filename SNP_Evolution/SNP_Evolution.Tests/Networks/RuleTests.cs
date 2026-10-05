using SnpEvolution.Networks;

namespace SnpEvolution.Tests.Networks
{
    public class RuleTests
    {
        [Theory]
        [InlineData("aa", 2, true)]
        [InlineData("aa", 3, false)]
        [InlineData("aa", 1, false)]
        [InlineData("a(aa)+", 5, true)]
        [InlineData("a(aa)+", 4, false)]
        public void ExpressionMustMatchTheWholeSpikeCount(string expression, long spikes, bool matches)
        {
            Assert.Equal(matches, new Rule(expression, 0, true).Matches(spikes));
        }
    }
}
