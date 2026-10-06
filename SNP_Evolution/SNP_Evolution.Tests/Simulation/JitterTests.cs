using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using static SnpEvolution.Tests.TestNetworks;

namespace SnpEvolution.Tests.Simulation
{
    public class JitterTests
    {
        // The output either fires or forgets on each spike, so runs differ and every random draw shows in the result.
        private static Network Coin() => new Network(new[]
        {
            InputNeuron(new[] { 2 }, Standard("a", 1)),
            new Neuron(new[] { Standard("a", 1), StandardForget("a", 1) }, 0, Array.Empty<int>(), true),
        });

        private static readonly InputSpikes Train = InputSpikes.Train(new[] { 0, 3, 4, 9, 12 });

        private static TrialResult Sample(Network network, SimulationOptions options, int seed = 3) =>
            new SequentialCpuEngine().Run(new[] { new Trial(network, Train, Readout.SpikeTrain) }, options, new Random(seed))[0];

        [Fact]
        public void NoJitterGivesTheSameRunsAsBefore()
        {
            var lockstep = new SimulationOptions(30, 20, OutputTiming.Interval);

            TrialResult before = Sample(Coin(), lockstep);
            TrialResult withoutJitter = Sample(Coin(), lockstep with { Jitter = 0 });

            Assert.Equal(before.SpikeTrains, withoutJitter.SpikeTrains);
            Assert.Equal(before.Outputs, withoutJitter.Outputs);
        }

        [Fact]
        public void JitterDelaysEachSpikeByUpToJSteps()
        {
            TrialResult result = Sample(Identity(), new SimulationOptions(30, 200, OutputTiming.Interval, Jitter: 2));

            // Lockstep, the output fires two steps after each input spike; the one synapse, input to output, may add up to 2 more.
            // Input from the environment is never late.
            Assert.All(result.SpikeTrains, train => Assert.All(train, step => Assert.Contains(Train.StepsPerInput[0], input => step - input is >= 2 and <= 4)));
            Assert.Equal(new[] { 2, 3, 4 }, result.SpikeTrains.Select(train => train[0]).Distinct().Order());
        }

        // The input spike reaches the output one to three steps after the input relays it; the run does not halt before.
        [Fact]
        public void LateSpikesKeepTheNetworkFromHalting()
        {
            var simulation = new NetworkSimulation(CompiledNetwork.Of(Identity()), new Random(1), InputSpikes.Train(new[] { 0 }), OutputTiming.Interval, recordSpikeTrain: true, jitter: 3);

            while (!simulation.IsHalted && simulation.StepCount < 20)
            {
                simulation.Step();
            }

            Assert.Single(simulation.OutputSpikeSteps);
        }

        [Fact]
        public void TheExhaustiveEngineRefusesJitter()
        {
            var trial = new Trial(Identity(), Train, Readout.SpikeTrain);

            Assert.Throws<ArgumentException>(() => new ExhaustiveCpuEngine().Run(new[] { trial }, new SimulationOptions(30, 5, OutputTiming.Interval, Jitter: 1), new Random(0)));
        }

        [Fact]
        public void JitterNeedsARandom()
        {
            Assert.Throws<ArgumentException>(() => new NetworkSimulation(CompiledNetwork.Of(Identity()), null, Train, OutputTiming.Interval, jitter: 1));
        }
    }
}
