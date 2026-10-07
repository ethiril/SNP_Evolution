using SnpEvolution.Model;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Model
{
    public class StandardRuleTests
    {
        [Fact]
        public void LegacyRulesAreNotStandardAndProduceOneSpike()
        {
            var rule = new Rule("aa", 0, true);

            Assert.False(rule.IsStandard);
            Assert.Null(rule.Consume);
            Assert.Equal(1, rule.Produce);
        }

        [Theory]
        [InlineData(null, 0, false, DelayKind.None)]
        [InlineData(null, 2, false, DelayKind.Holding)]
        [InlineData(2L, 2, false, DelayKind.Closing)]
        [InlineData(null, 2, true, DelayKind.Axonal)]
        [InlineData(2L, 2, true, DelayKind.Axonal)]
        [InlineData(2L, 0, true, DelayKind.None)]
        public void TheDelayKindFollowsTheFormAndWhetherTheDelayIsAxonal(long? consume, int delay, bool axonal, DelayKind kind)
        {
            Assert.Equal(kind, new Rule("a+", delay, true, consume, axonal: axonal).DelayKind);
        }

        // A standard rule made legacy keeps its Produce, but a legacy rule always sends one.
        [Fact]
        public void SendsIsWhatTheRuleSendsAlongEachSynapse()
        {
            Rule standard = Standard("a+", 1, produce: 3);

            Assert.Equal((3, 1, 0), (standard.Sends, standard.WithConsume(null).Sends, standard.WithFire(false).Sends));
        }

        [Fact]
        public void LeastHeldIsWhatAStandardRuleConsumes()
        {
            Assert.Equal((2L, 0L), (Standard("a+", 2).LeastHeld, new Rule("a+", 0, true).LeastHeld));
        }
    }
}
