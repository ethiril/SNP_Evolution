using SnpEvolution.Networks;
using static SnpEvolution.Tests.TestNetworks;

namespace SnpEvolution.Tests.Networks
{
    public class NetworkSimulationTests
    {
        [Fact]
        public void OutputIsTheStepCountBetweenTheOutputNeuronsFirstAndSecondSpike()
        {
            var simulation = new NetworkSimulation(AlwaysOutputsOne(), new Random(0));

            simulation.Step();
            Assert.Null(simulation.Output);
            Assert.Equal(new[] { "a", "a" }, simulation.Spikes);

            simulation.Step();
            Assert.Equal(1, simulation.Output);
            Assert.Equal(2, simulation.StepCount);
        }

        [Fact]
        public void OutputCountsTheSilentStepsBeforeTheSecondSpike()
        {
            var network = new Network(new[]
            {
                Neuron("a", new[] { 2 }, new Rule("a", 0, true)),
                Neuron("", new[] { 3 }, new Rule("a", 0, true)),
                OutputNeuron("a", new Rule("a", 0, true)),
            });
            var simulation = new NetworkSimulation(network, new Random(0));

            simulation.Step();
            simulation.Step();
            Assert.Null(simulation.Output);

            simulation.Step();
            Assert.Equal(2, simulation.Output);
        }

        [Fact]
        public void SpikesFromSeveralNeuronsInTheSameStepAllArrive()
        {
            var network = new Network(new[]
            {
                Neuron("a", new[] { 3 }, new Rule("a", 0, true)),
                Neuron("a", new[] { 3 }, new Rule("a", 0, true)),
                Neuron("", Array.Empty<int>(), new Rule("aaaa", 0, true)),
            });
            var simulation = new NetworkSimulation(network, new Random(0));

            simulation.Step();

            Assert.Equal(new[] { "", "", "aa" }, simulation.Spikes);
        }

        [Fact]
        public void RuleIsChosenOnlyAmongRulesThatMatch()
        {
            var network = new Network(new[]
            {
                Neuron("a", new[] { 2 }, new Rule("aaa", 0, false), new Rule("a", 0, true), new Rule("a", 0, true)),
                Neuron("", Array.Empty<int>(), new Rule("aaaa", 0, true)),
            });

            for (int seed = 0; seed < 50; seed++)
            {
                var simulation = new NetworkSimulation(network, new Random(seed));
                simulation.Step();
                Assert.Equal("a", simulation.Spikes[1]);
            }
        }

        [Fact]
        public void ForgettingRuleConsumesSpikesWithoutEmitting()
        {
            var network = new Network(new[]
            {
                Neuron("aa", new[] { 2 }, new Rule("aa", 0, false)),
                Neuron("", Array.Empty<int>(), new Rule("aaaa", 0, true)),
            });
            var simulation = new NetworkSimulation(network, new Random(0));

            simulation.Step();

            Assert.Equal(new[] { "", "" }, simulation.Spikes);
        }

        [Fact]
        public void DelayedRuleHoldsSpikesUntilTheDelayElapses()
        {
            var network = new Network(new[]
            {
                Neuron("a", Array.Empty<int>(), new Rule("a", 2, true)),
            });
            var simulation = new NetworkSimulation(network, new Random(0));

            simulation.Step();
            simulation.Step();
            Assert.Equal("a", simulation.Spikes[0]);

            simulation.Step();
            Assert.Equal("a", simulation.Spikes[0]);

            simulation.Step();
            Assert.Equal("", simulation.Spikes[0]);
        }
    }
}
