using SnpEvolution.Model;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Model
{
    public class DeterministicNeuronsTests
    {
        [Fact]
        public void ConformKeepsTheFirstOfTwoRivalRules()
        {
            Rule first = Standard("a+", 1), rival = Standard("aa", 2), apart = Standard("a", 1, produce: 2);
            var network = new Network(new[] { OutputNeuron(0, first, rival), OutputNeuron(0, Standard("aa", 2), apart) });

            Network conformed = DeterministicNeurons.Conform(network);

            Assert.False(DeterministicNeurons.Fits(network));
            Assert.True(DeterministicNeurons.Fits(conformed));
            Assert.Equal(new[] { first }, conformed.Neurons[0].Rules);
            Assert.Same(network.Neurons[1], conformed.Neurons[1]);
        }

        [Fact]
        public void ConformReturnsANetworkThatFitsUnchanged()
        {
            Network network = AlwaysOutputsOne();

            Assert.Same(network, DeterministicNeurons.Conform(network));
        }
    }
}
