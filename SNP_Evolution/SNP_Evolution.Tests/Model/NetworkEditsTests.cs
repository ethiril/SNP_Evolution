using SnpEvolution.Model;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Model
{
    public class NetworkEditsTests
    {
        [Fact]
        public void RemoveNeuronRenumbersTheRemainingSynapses()
        {
            var network = new Network(new[]
            {
                Neuron(1, new[] { 2, 3 }, new Rule("a", 0, true)),
                Neuron(1, new[] { 3 }, new Rule("a", 0, true)),
                OutputNeuron(0, new Rule("a", 0, true)),
            });

            Network removed = NetworkEdits.RemoveNeuron(network, 1);

            Assert.Equal(2, removed.Neurons.Count);
            Assert.Equal(new[] { 2 }, removed.Neurons[0].Connections);
        }
    }
}
