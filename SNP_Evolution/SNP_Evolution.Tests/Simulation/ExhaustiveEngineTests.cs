using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using static SnpEvolution.Tests.TestNetworks;

namespace SnpEvolution.Tests.Simulation
{
    public class ExhaustiveEngineTests
    {
        private static readonly SimulationOptions Options = new SimulationOptions(MaxSteps: 30, Repetitions: 200);

        [Fact]
        public void DeterministicNetworkHasOneExactOutput()
        {
            TrialResult result = new ExhaustiveCpuEngine().Run(new[] { Trial.Generate(AlwaysOutputsOne()) }, Options, new Random(0))[0];

            Assert.True(result.Exact);
            Assert.Equal(new[] { 1 }, result.Outputs);
        }

        [Fact]
        public void EverySampledOutputIsAmongTheExactOutputs()
        {
            foreach (Network network in new[] { ReferenceNetworks.NaturalNumbers(), ReferenceNetworks.EvenNumbers() })
            {
                TrialResult exact = new ExhaustiveCpuEngine().Run(new[] { Trial.Generate(network) }, Options, new Random(0))[0];
                TrialResult sampled = new SequentialCpuEngine().Run(new[] { Trial.Generate(network) }, Options, new Random(1))[0];

                Assert.True(exact.Exact);
                Assert.Equal(exact.Outputs.Distinct().OrderBy(output => output), exact.Outputs);
                Assert.Subset(exact.Outputs.ToHashSet(), sampled.Outputs.ToHashSet());
            }
        }

        [Fact]
        public void FindsEveryOutputOfTheEvenNumbersNetwork()
        {
            TrialResult result = new ExhaustiveCpuEngine().Run(new[] { Trial.Generate(ReferenceNetworks.EvenNumbers()) }, Options, new Random(0))[0];

            Assert.NotEmpty(result.Outputs);
            Assert.All(result.Outputs, output => Assert.Equal(0, output % 2));
            Assert.True(result.Outputs.Count >= 5);
        }

        [Fact]
        public void HaltingReadoutFindsAComputationThatHalts()
        {
            // Each step the neuron either keeps its spike or forgets it, so only some computations halt.
            var network = new Network(new[] { Neuron(1, new[] { 1 }, new Rule("a", 0, false), new Rule("a", 0, true)) });
            var selfLoop = new Network(new[] { Neuron(1, new[] { 2 }, Standard("a", 1)), Neuron(0, new[] { 1 }, Standard("a", 1)) });
            var engine = new ExhaustiveCpuEngine();

            IReadOnlyList<TrialResult> results = engine.Run(new[]
            {
                new Trial(network, InputSpikes.None, Readout.Halting),
                new Trial(selfLoop, InputSpikes.None, Readout.Halting),
            }, Options, new Random(0));

            Assert.True(results[0].CanHalt);
            Assert.False(results[1].CanHalt);
            Assert.True(results[1].Exact);
        }

        [Fact]
        public void NetworkThatCyclesAfterItsInputIsSettledAsNeverHalting()
        {
            // Two neurons pass a spike back and forth forever; the repeated configuration ends the search early and exactly.
            var network = new Network(new[]
            {
                InputNeuron(new[] { 2 }, Standard("a", 1)),
                Neuron(0, new[] { 1 }, Standard("a", 1)),
            });

            TrialResult result = new ExhaustiveCpuEngine(maxConfigurations: 4).Run(
                new[] { new Trial(network, InputSpikes.Numbers(3), Readout.Halting) }, Options, new Random(0))[0];

            Assert.True(result.Exact);
            Assert.False(result.CanHalt);
        }

        [Fact]
        public void NetworkThatCyclesAfterItsInputIsSettledAsNeverHalting()
        {
            // Two neurons pass a spike back and forth forever; the repeated configuration ends the search early and exactly.
            var network = new Network(new[]
            {
                InputNeuron(new[] { 2 }, Standard("a", 1)),
                Neuron(0, new[] { 1 }, Standard("a", 1)),
            });

            TrialResult result = new ExhaustiveCpuEngine(maxConfigurations: 4).Run(
                new[] { new Trial(network, InputSpikes.Numbers(3), Readout.Halting) }, Options, new Random(0))[0];

            Assert.True(result.Exact);
            Assert.False(result.CanHalt);
        }

        [Fact]
        public void FallsBackToSamplingWhenTheComputationTreeIsTooWide()
        {
            TrialResult result = new ExhaustiveCpuEngine(maxConfigurations: 1).Run(new[] { Trial.Generate(ReferenceNetworks.NaturalNumbers()) }, Options, new Random(0))[0];

            Assert.False(result.Exact);
            Assert.NotEmpty(result.Outputs);
        }
    }
}
