using SnpEvolution.Model;
using SnpEvolution.Simulation;
using static SnpEvolution.Tests.Fixtures.Runs;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Simulation
{
    public class StandardSemanticsTests
    {
        [Fact]
        public void ConsumesExactlyItsSpikesAndSendsWhatItProduces()
        {
            var network = new Network(new[]
            {
                Neuron(5, new[] { 2 }, Standard("a+", 2, produce: 3)),
                Sink(),
            });
            NetworkSimulation simulation = Simulate(network);

            simulation.Step();

            Assert.Equal(new long[] { 3, 3 }, simulation.Spikes);
        }

        [Fact]
        public void ForgettingRuleConsumesWithoutSending()
        {
            var network = new Network(new[]
            {
                Neuron(3, new[] { 2 }, StandardForget("aaa", 2)),
                Sink(),
            });
            NetworkSimulation simulation = Simulate(network);

            simulation.Step();

            Assert.Equal(new long[] { 1, 0 }, simulation.Spikes);
        }

        [Fact]
        public void DelayedRuleClosesTheNeuronLosingIncomingSpikesAndEmitsWhenItReopens()
        {
            // n1 fires with delay 2 at step 0 and emits at step 2; n2 sends n1 a spike at every step.
            var network = new Network(new[]
            {
                Neuron(1, new[] { 3 }, Standard("a", 1, delay: 2)),
                Neuron(3, new[] { 1 }, Standard("a+", 1)),
                Sink(),
            });
            NetworkSimulation simulation = Simulate(network);

            simulation.Step();
            Assert.Equal(new long[] { 0, 2, 0 }, simulation.Spikes);
            simulation.Step();
            Assert.Equal(new long[] { 0, 1, 0 }, simulation.Spikes);
            simulation.Step();
            Assert.Equal(new long[] { 1, 0, 1 }, simulation.Spikes);
        }

        [Fact]
        public void InputSpikesArriveAtTheirStepsOnTheInputNeurons()
        {
            var network = new Network(new[]
            {
                InputNeuron(Array.Empty<int>(), Standard("a{100}", 100)),
                InputNeuron(Array.Empty<int>(), Standard("a{100}", 100)),
            });
            NetworkSimulation simulation = Simulate(network, new InputSpikes(new[] { new[] { 0, 2 }, new[] { 1 } }));

            simulation.Step();
            Assert.Equal(new long[] { 1, 0 }, simulation.Spikes);
            simulation.Step();
            simulation.Step();
            Assert.Equal(new long[] { 2, 1 }, simulation.Spikes);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(9)]
        public void IdentityNetworkOutputsItsInputAsAnInterval(int number)
        {
            NetworkSimulation simulation = Simulate(Identity(), InputSpikes.Numbers(number));

            while (simulation.Output == null && simulation.StepCount < 50)
            {
                simulation.Step();
            }

            Assert.Equal(number, simulation.Output);
        }

        [Fact]
        public void IntervalTimingIgnoresTheStepsBeforeTheFirstSpike()
        {
            // The output neuron first fires at step 2 and again at step 3.
            var network = new Network(new[]
            {
                Neuron(2, new[] { 2 }, Standard("a+", 1)),
                Neuron(0, new[] { 3 }, Standard("a", 1)),
                OutputNeuron(0, Standard("a", 1)),
            });
            NetworkSimulation interval = Simulate(network);
            NetworkSimulation legacy = Simulate(network, timing: OutputTiming.Legacy);
            for (int step = 0; step < 4; step++)
            {
                interval.Step();
                legacy.Step();
            }

            Assert.Equal(1, interval.Output);
            Assert.Equal(3, legacy.Output);
        }

        [Fact]
        public void HaltsOnceNothingCanHappenAndTheInputIsSpent()
        {
            var network = new Network(new[] { InputNeuron(Array.Empty<int>(), StandardForget("a", 1)) });
            NetworkSimulation simulation = Simulate(network, InputSpikes.Numbers(2));

            Assert.False(simulation.IsHalted);
            simulation.Step();
            simulation.Step();
            Assert.False(simulation.IsHalted);
            simulation.Step();
            Assert.False(simulation.IsHalted);
            simulation.Step();
            Assert.True(simulation.IsHalted);
        }

        [Fact]
        public void LegacyAndStandardRulesMixInOneNetwork()
        {
            var network = new Network(new[]
            {
                Neuron(4, new[] { 2 }, Standard("a+", 1, produce: 2)),
                Neuron(1, Array.Empty<int>(), new Rule("a", 0, false)),
            });
            NetworkSimulation simulation = Simulate(network);

            simulation.Step();

            Assert.Equal(new long[] { 3, 2 }, simulation.Spikes);
        }
    }
}
