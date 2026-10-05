using SnpEvolution.Networks;

namespace SnpEvolution.Tests.Networks
{
    public class RuleTests
    {
        [Theory]
        [InlineData("aa", "aa", true)]
        [InlineData("aa", "aaa", false)]
        [InlineData("aa", "a", false)]
        [InlineData("a(aa)+", "aaaaa", true)]
        [InlineData("a(aa)+", "aaaa", false)]
        public void ExpressionMustMatchTheWholeSpikeString(string expression, string spikes, bool matches)
        {
            Assert.Equal(matches, new Rule(expression, 0, true).Matches(spikes));
        }
    }
}
