using SnpEvolution.Model;
using SnpEvolution.Simulation;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Simulation
{
    public class SpikeTrainTests
    {
        private static readonly SimulationOptions Options = new SimulationOptions(MaxSteps: 10, Repetitions: 3, OutputTiming.Interval);

        private static Trial SpikeTrainOf(Network network) => new Trial(network, InputSpikes.None, Readout.SpikeTrain);

        [Fact]
        public void PingPongOutputFiresOnEveryOtherStep()
        {
            var simulation = new NetworkSimulation(CompiledNetwork.Of(PingPong()), new Random(0), InputSpikes.None, OutputTiming.Interval, recordSpikeTrain: true);

            for (int step = 0; step < 8; step++)
            {
                simulation.Step();
            }

            Assert.Equal(new[] { 1, 3, 5, 7 }, simulation.OutputSpikeSteps);
        }

        [Fact]
        public void SpikeTrainIsEmptyUnlessRecorded()
        {
            var simulation = new NetworkSimulation(CompiledNetwork.Of(PingPong()), new Random(0), InputSpikes.None, OutputTiming.Interval);

            simulation.Step();
            simulation.Step();

            Assert.Empty(simulation.OutputSpikeSteps);
        }

        [Fact]
        public void SamplingRunsPastTheSecondSpikeAndKeepsEveryRunsTrain()
        {
            TrialResult result = NetworkRunner.Sample(SpikeTrainOf(PingPong()), Options, new Random(0));

            Assert.Equal(3, result.SpikeTrains.Count);
            Assert.All(result.SpikeTrains, train => Assert.Equal(new[] { 1, 3, 5, 7, 9 }, train));
            Assert.Equal(new[] { 2, 2, 2 }, result.Outputs);
        }

        [Fact]
        public void ExhaustiveEngineReportsADeterministicTrainOnce()
        {
            TrialResult result = new ExhaustiveCpuEngine().Run(new[] { SpikeTrainOf(PingPong()) }, Options, new Random(0))[0];

            Assert.True(result.Exact);
            Assert.Equal(new[] { 1, 3, 5, 7, 9 }, Assert.Single(result.SpikeTrains));
            Assert.Equal(new[] { 2 }, result.Outputs);
        }

        // The output either fires at once or waits a step and fires, so there are two trains, each reported once.
        [Fact]
        public void ExhaustiveEngineReportsEachDistinctTrain()
        {
            var network = new Network(new[] { OutputNeuron(1, new Rule("a", 0, true), new Rule("a", 1, true)) });

            TrialResult result = new ExhaustiveCpuEngine().Run(new[] { SpikeTrainOf(network) }, Options, new Random(0))[0];

            Assert.True(result.Exact);
            Assert.Equal(2, result.SpikeTrains.Count);
            Assert.Contains(result.SpikeTrains, train => train.SequenceEqual(new[] { 0 }));
        }

        [Fact]
        public void OtherReadoutsHaveNoSpikeTrains()
        {
            TrialResult result = NetworkRunner.Sample(Trial.Generate(PingPong()), Options, new Random(0));

            Assert.Empty(result.SpikeTrains);
        }
    }
}
