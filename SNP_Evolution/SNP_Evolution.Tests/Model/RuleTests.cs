using SnpEvolution.Model;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Model
{
    public class RuleTests
    {
        // A null consume is a legacy rule, which needs only the expression to match the whole spike count.
        [Theory]
        [InlineData("aa", null, 2, true)]
        [InlineData("aa", null, 3, false)]
        [InlineData("aa", null, 1, false)]
        [InlineData("a(aa)+", null, 5, true)]
        [InlineData("a(aa)+", null, 4, false)]
        [InlineData("a(aa)*", 2L, 3, true)]
        [InlineData("a(aa)*", 2L, 1, false)]
        [InlineData("a(aa)*", 2L, 4, false)]
        [InlineData("a+", 3L, 3, true)]
        public void AppliesOnlyWhenTheExpressionMatchesTheWholeCountAndEnoughSpikesAreThere(string expression, long? consume, long spikes, bool applies)
        {
            Rule rule = consume is long standard ? Standard(expression, standard) : new Rule(expression, 0, true);

            Assert.Equal(applies, rule.Applies(spikes));
        }
    }
}
