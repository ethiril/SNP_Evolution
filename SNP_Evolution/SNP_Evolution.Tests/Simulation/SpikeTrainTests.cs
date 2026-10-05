using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using static SnpEvolution.Tests.TestNetworks;

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
        public void ExhaustiveEngineSamplesSpikeTrains()
        {
            TrialResult result = new ExhaustiveCpuEngine().Run(new[] { SpikeTrainOf(PingPong()) }, Options, new Random(0))[0];

            Assert.False(result.Exact);
            Assert.Equal(3, result.SpikeTrains.Count);
        }

        [Fact]
        public void OtherReadoutsHaveNoSpikeTrains()
        {
            TrialResult result = NetworkRunner.Sample(Trial.Generate(PingPong()), Options, new Random(0));

            Assert.Empty(result.SpikeTrains);
        }
    }
}
