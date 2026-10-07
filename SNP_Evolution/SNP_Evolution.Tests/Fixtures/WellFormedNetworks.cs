using SnpEvolution.Model;
using SnpEvolution.Search.Genome;

namespace SnpEvolution.Tests.Fixtures
{
    internal static class WellFormedNetworks
    {
        // Up to six neurons of any rule form, with duplicated neurons allowed, so every structural edit has room.
        public static NetworkFactory SmallFactory(int seed, int inputCount = 1, RuleForm form = RuleForm.Mixed) =>
            Factories.Networks(new GenomeSpace(InputCount: inputCount, RuleForm: form, MaxNeurons: 6, DuplicateNeurons: true), new Random(seed), expressions: new Random(seed));

        // What every network a factory or an edit makes must satisfy.
        public static void AssertWellFormed(Network network, GenomeSpace space)
        {
            Assert.Single(network.Neurons, neuron => neuron.IsOutput);
            Assert.Equal(space.InputCount, network.Neurons.Count(neuron => neuron.IsInput));
            Assert.InRange(network.Neurons.Count, space.SmallestNetwork, space.MaxNeurons);
            for (int position = 1; position <= network.Neurons.Count; position++)
            {
                Neuron neuron = network.Neurons[position - 1];
                Assert.NotEmpty(neuron.Rules);
                Assert.InRange(neuron.Rules.Count, 1, space.MaxRulesPerNeuron);
                Assert.DoesNotContain(position, neuron.Connections);
                Assert.All(neuron.Connections, target => Assert.InRange(target, 1, network.Neurons.Count));
                Assert.Equal(neuron.Connections.Distinct().OrderBy(target => target), neuron.Connections);
                Assert.All(neuron.Rules.Where(rule => rule.IsStandard), rule => Assert.True(rule.Consume >= 1 && rule.Produce >= 1));
                Assert.True(neuron.InitialSpikes >= 0);
                Assert.True(!neuron.IsInput || neuron.InitialSpikes == 0);
            }
        }
    }
}
