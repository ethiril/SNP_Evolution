using SnpEvolution.Model;
using SnpEvolution.Search.Genome;

namespace SnpEvolution.Tests.Search.Genome
{
    public class RandomTopologyTests
    {
        [Fact]
        public void RandomTopologyHasOneOutputAndValidConnections()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var random = new Random(seed);
                Network network = RandomTopology.Create(new ExpressionGenerator(ExpressionGenerator.SimpleTemplates, 4, random), 4, random);

                Assert.Single(network.Neurons, neuron => neuron.IsOutput);
                for (int position = 1; position <= network.Neurons.Count; position++)
                {
                    IReadOnlyList<int> connections = network.Neurons[position - 1].Connections;
                    Assert.NotEmpty(connections);
                    Assert.DoesNotContain(position, connections);
                    Assert.All(connections, target => Assert.InRange(target, 1, network.Neurons.Count));
                    Assert.Equal(connections.Distinct().OrderBy(target => target), connections);
                }
            }
        }
    }
}
