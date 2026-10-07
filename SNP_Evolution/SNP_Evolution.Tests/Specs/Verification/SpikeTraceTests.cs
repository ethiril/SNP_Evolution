using SnpEvolution.Model;
using SnpEvolution.Specs.Verification;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Specs.Verification
{
    public class SpikeTraceTests
    {
        [Fact]
        public void FindsRulesThatOnlyCompeteOnceTheNeuronHoldsWhatTheyConsume()
        {
            var network = new Network(new[] { OutputNeuron(0, Standard("a+", 5), Standard("a+", 5, produce: 2)) });

            Assert.Contains("when it holds 5 spike(s)", Assert.Single(SpikeTrace.Choices(network)));
        }
    }
}
