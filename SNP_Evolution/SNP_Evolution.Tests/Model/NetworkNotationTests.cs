using SnpEvolution.Model;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Model
{
    public class NetworkNotationTests
    {
        [Theory]
        [InlineData(0, "-")]
        [InlineData(3, "aaa")]
        [InlineData(10, "aaaaaaaaaa")]
        [InlineData(11, "a^11")]
        [InlineData(5_000_000_000, "a^5000000000")]
        public void WritesSpikeCountsAsRunsOfA(long count, string notation)
        {
            Assert.Equal(notation, NetworkNotation.Spikes(count));
        }

        [Fact]
        public void FormatsTheNaturalNumbersNetworkAsATable()
        {
            string expected = string.Join(Environment.NewLine,
                "Neuron     Spikes   Rules                       Sends to",
                "n1         aa       aa -> a  |  a -> forget     n2, n3, n4",
                "n2         aa       aa -> a  |  a -> forget;1   n1, n3, n4",
                "n3         aa       aa -> a  |  aa -> a;1       n1, n2, n4",
                "n4 (out)   aa       aa -> a  |  aaa -> forget   -",
                "");

            Assert.Equal(expected, NetworkNotation.Format(ReferenceNetworks.NaturalNumbers()));
        }

        [Theory]
        [InlineData("a(aa)*", 1L, 1, 0, true, "a(aa)*/a -> a")]
        [InlineData("aaa", 3L, 2, 0, true, "aaa -> aa")]
        [InlineData("a+", 2L, 1, 2, true, "a+/aa -> a;2")]
        [InlineData("aa", 2L, 1, 0, false, "aa -> forget")]
        public void IsWrittenInSnpNotation(string expression, long consume, int produce, int delay, bool fire, string notation)
        {
            Assert.Equal(notation, NetworkNotation.Rule(new Rule(expression, delay, fire, consume, produce)));
        }

        [Fact]
        public void ALegacyRuleIsWrittenSendingOneWhateverItsProduce()
        {
            Assert.Equal("a+ -> a", NetworkNotation.Rule(new Rule("a+", 0, true, produce: 3)));
        }

        [Fact]
        public void TableMarksInputNeurons()
        {
            Assert.StartsWith("n1 (in)", NetworkNotation.Format(Identity()).Split(Environment.NewLine)[1]);
        }
    }
}
