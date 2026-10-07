using SnpEvolution.Model;
using SnpEvolution.Search.Fitness;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Search.Fitness
{
    public class NetworkFingerprintTests
    {
        [Fact]
        public void EqualNetworksBuiltApartShareAFingerprint()
        {
            Assert.Equal(NetworkFingerprint.Of(Identity()), NetworkFingerprint.Of(Identity()));
        }

        [Fact]
        public void AModuleTagDoesNotChangeIt()
        {
            Network network = Identity();
            Network tagged = network.WithNeuron(0, network.Neurons[0].WithModule(new ModuleTag(1, 0)));

            Assert.Equal(NetworkFingerprint.Of(network), NetworkFingerprint.Of(tagged));
        }

        [Fact]
        public void ChangingWhatANetworkDoesChangesIt()
        {
            Network network = Identity();
            ulong fingerprint = NetworkFingerprint.Of(network);
            Neuron first = network.Neurons[0];

            Assert.NotEqual(fingerprint, NetworkFingerprint.Of(network.WithNeuron(0, first.WithInitialSpikes(first.InitialSpikes + 1))));
            Assert.NotEqual(fingerprint, NetworkFingerprint.Of(network.WithRule(0, 0, first.Rules[0].WithExpression("aa"))));
            Assert.NotEqual(fingerprint, NetworkFingerprint.Of(network.WithNeuron(0, first.WithRoles(!first.IsOutput, first.IsInput))));
            Assert.NotEqual(fingerprint, NetworkFingerprint.Of(AlwaysOutputsOne()));
        }
    }
}
