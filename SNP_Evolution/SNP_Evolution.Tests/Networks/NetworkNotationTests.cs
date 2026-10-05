using SnpEvolution.Networks;

namespace SnpEvolution.Tests.Networks
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
    }
}
