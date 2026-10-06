using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Simulation.Metal;
using SnpEvolution.Storage;
using static SnpEvolution.Tests.TestNetworks;

namespace SnpEvolution.Tests.Simulation
{
    public class AxonalDelayTests
    {
        private static Rule Axonal(string expression, int delay) => new Rule(expression, delay, true, axonal: true);

        private static NetworkSimulation Simulate(Network network) =>
            new NetworkSimulation(CompiledNetwork.Of(network), new Random(0), InputSpikes.None, OutputTiming.Interval);

        [Fact]
        public void ConsumesAtOnceStaysOpenAndSendsDelayStepsLater()
        {
            // n1 fires on every step, as n2 refills it; each spike reaches n3 two steps after the step it fired on.
            var network = new Network(new[]
            {
                Neuron(1, new[] { 3 }, Axonal("a+", 2)),
                Neuron(3, new[] { 1 }, Standard("a+", 1)),
                Neuron(0, Array.Empty<int>(), Standard("a{100}", 100)),
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
                Neuron(0, Array.Empty<int>(), Standard("a{100}", 100)),
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
            Assert.NotEqual(first.State(), second.State());
        }

        [Fact]
        public void OnlyDelayedAxonalRulesNeedTheCpu()
        {
            var plain = new Network(new[] { OutputNeuron(1, Axonal("a", 0)) });
            var delayed = new Network(new[] { OutputNeuron(1, Axonal("a", 1)) });

            Assert.False(plain.Neurons[0].Rules[0].Axonal);
            Assert.True(GpuNetwork.Of(CompiledNetwork.Of(plain)).IsSupported);
            Assert.False(GpuNetwork.Of(CompiledNetwork.Of(delayed)).IsSupported);
        }

        [Fact]
        public void AnAxonalRuleIsADifferentRuleFromTheSameRuleThatHoldsTheNeuron()
        {
            var network = new Network(new[] { Neuron(0, new[] { 2 }, Axonal("a+", 2)), Neuron(0, Array.Empty<int>(), new Rule("a+", 2, true)) });

            Assert.NotEqual(network.Neurons[0].Rules[0].Key, network.Neurons[1].Rules[0].Key);
            Assert.Equal(2, SnpEvolution.Evolution.HardwareCost.Of(network).DistinctRules);
        }

        [Fact]
        public void SavesTheAxonalFlagOnlyWhenSet()
        {
            var network = new Network(new[] { Neuron(1, new[] { 2 }, Axonal("a+", 2)), OutputNeuron(0, new Rule("a", 1, true)) });

            string json = NetworkFiles.ToJson(network);
            Network loaded = Assert.IsType<Network>(NetworkFiles.FromJson(json));

            Assert.True(loaded.Neurons[0].Rules[0].Axonal);
            Assert.False(loaded.Neurons[1].Rules[0].Axonal);
            Assert.Single(json.Split("Axonal").Skip(1));
            Assert.Equal("a+ -> a;2 axonal", NetworkNotation.Rule(loaded.Neurons[0].Rules[0]));
        }
    }
}
