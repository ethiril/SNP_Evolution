using SnpEvolution.Networks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Tests.Simulation
{
    public class SimulationEngineTests
    {
        private static readonly SimulationOptions Options = new SimulationOptions(MaxSteps: 50, Repetitions: 50);

        private static Network[] Batch() =>
            new[] { ReferenceNetworks.NaturalNumbers(), TestNetworks.NeverOutputs(), ReferenceNetworks.EvenNumbers(), TestNetworks.AlwaysOutputsOne() };

        public static TheoryData<ISimulationEngine> Engines => new TheoryData<ISimulationEngine> { new SequentialCpuEngine(), new ParallelCpuEngine() };

        [Theory]
        [MemberData(nameof(Engines))]
        public void ReturnsOneSortedOutputListPerNetworkInOrder(ISimulationEngine engine)
        {
            IReadOnlyList<IReadOnlyList<int>> outputs = engine.CollectOutputs(Batch(), Options, new Random(5));

            Assert.Equal(4, outputs.Count);
            Assert.All(outputs, list => Assert.Equal(list.OrderBy(output => output), list));
            Assert.All(outputs[0], output => Assert.True(output > 0));
            Assert.Empty(outputs[1]);
            Assert.All(outputs[2], output => Assert.Equal(0, output % 2));
            Assert.Equal(Enumerable.Repeat(1, 50), outputs[3]);
        }

        [Fact]
        public void ParallelEngineIsReproducibleFromTheSeed()
        {
            var engine = new ParallelCpuEngine();

            IReadOnlyList<IReadOnlyList<int>> first = engine.CollectOutputs(Batch(), Options, new Random(9));
            IReadOnlyList<IReadOnlyList<int>> second = engine.CollectOutputs(Batch(), Options, new Random(9));

            Assert.Equal(first, second);
        }

        [Fact]
        public void HugeSpikeCountsRunAsFastAsSmallOnes()
        {
            // Two neurons pass a spike back and forth while a third, holding billions, waits to reach an odd count.
            var network = new Network(new[]
            {
                TestNetworks.Neuron(1, new[] { 2, 3 }, new Rule("a", 0, true)),
                TestNetworks.Neuron(0, new[] { 1, 3 }, new Rule("a", 0, true)),
                TestNetworks.OutputNeuron(4_000_000_000, new Rule("a(aa)+", 0, true)),
            });
            var simulation = new NetworkSimulation(network, new Random(0));

            simulation.Step();

            Assert.Equal(4_000_000_001, simulation.Spikes[2]);
            // The odd count fires and empties the neuron, which then receives the spike passed back this step.
            simulation.Step();
            Assert.Equal(1, simulation.Spikes[2]);
        }
    }
}
