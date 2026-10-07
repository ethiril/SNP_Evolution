using SnpEvolution.Model;
using SnpEvolution.Simulation;
using static SnpEvolution.Tests.Fixtures.Runs;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Simulation
{
    public class AxonalDelayTests
    {
        [Fact]
        public void ConsumesAtOnceStaysOpenAndSendsDelayStepsLater()
        {
            // n1 fires on every step, as n2 refills it; each spike reaches n3 two steps after the step it fired on.
            var network = new Network(new[]
            {
                Neuron(1, new[] { 3 }, Axonal("a+", 2)),
                Neuron(3, new[] { 1 }, Standard("a+", 1)),
                Sink(),
            });
            NetworkSimulation simulation = Simulate(network);

            simulation.Step();
            Assert.Equal(new long[] { 1, 2, 0 }, simulation.Spikes);
            simulation.Step();
            Assert.Equal(new long[] { 1, 1, 0 }, simulation.Spikes);
            simulation.Step();
            Assert.Equal(new long[] { 1, 0, 1 }, simulation.Spikes);
            simulation.Step();
            Assert.Equal(new long[] { 0, 0, 2 }, simulation.Spikes);
        }

        [Fact]
        public void SpikesInFlightKeepTheNetworkFromHalting()
        {
            var network = new Network(new[]
            {
                Neuron(1, new[] { 2 }, Axonal("a", 3)),
                Sink(),
            });
            NetworkSimulation simulation = Simulate(network);

            simulation.Step();
            Assert.Equal(new long[] { 0, 0 }, simulation.Spikes);
            Assert.False(simulation.IsHalted);
            simulation.Step();
            simulation.Step();
            Assert.False(simulation.IsHalted);
            simulation.Step();
            Assert.Equal(new long[] { 0, 1 }, simulation.Spikes);
            Assert.True(simulation.IsHalted);
        }

        [Fact]
        public void TheStateTellsSpikesInFlightApart()
        {
            // Both networks hold nothing after a step, but only the first has a spike on its way.
            var delayed = new Network(new[] { Neuron(1, new[] { 2 }, Axonal("a", 2)), Neuron(0, Array.Empty<int>(), Axonal("a{100}", 2)) });
            var forgotten = new Network(new[] { Neuron(1, new[] { 2 }, new Rule("a", 0, false)), Neuron(0, Array.Empty<int>(), Axonal("a{100}", 2)) });
            NetworkSimulation first = Simulate(delayed);
            NetworkSimulation second = Simulate(forgotten);

            first.Step();
            second.Step();

            Assert.Equal(first.Spikes, second.Spikes);
            Assert.NotEqual(first.Current.State(), second.Current.State());
        }

        [Fact]
        public void OnlyDelayedAxonalRulesNeedTheCpu()
        {
            var plain = new Network(new[] { OutputNeuron(1, Axonal("a", 0)) });
            var delayed = new Network(new[] { OutputNeuron(1, Axonal("a", 1)) });

            Assert.False(plain.Neurons[0].Rules[0].Axonal);
            EngineSupport gpu = new EngineSupport(Jitter: false, AxonalDelay: false, Ports: false, EveryComputation: false);
            Assert.True(gpu.Runs(Trial.Generate(plain), new SimulationOptions(10, 1)));
            Assert.False(gpu.Runs(Trial.Generate(delayed), new SimulationOptions(10, 1)));
        }
    }
}
