using SnpEvolution.Model;
using SnpEvolution.Simulation;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Simulation
{
    public class SimulationEngineTests
    {
        public static TheoryData<ISimulationEngine> Engines => new TheoryData<ISimulationEngine> { new SequentialCpuEngine(), new ParallelCpuEngine() };

        private static readonly SimulationOptions Options = new SimulationOptions(MaxSteps: 50, Repetitions: 50);

        private static Network[] Batch() =>
            new[] { ReferenceNetworks.NaturalNumbers(), NeverOutputs(), ReferenceNetworks.EvenNumbers(), AlwaysOutputsOne() };

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
        public void ParallelEngineSplitsRunsWithoutLosingAny()
        {
            // 20 runs make three chunks, the last one short.
            IReadOnlyList<IReadOnlyList<int>> outputs = new ParallelCpuEngine().CollectOutputs(Batch(), Options with { Repetitions = 20 }, new Random(2));

            Assert.Equal(Enumerable.Repeat(1, 20), outputs[3]);
            Assert.Equal(20, outputs[0].Count);
            Assert.Empty(new ParallelCpuEngine().CollectOutputs(Batch(), Options with { Repetitions = 0 }, new Random(2))[3]);
        }
    }
}
