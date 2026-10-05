using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using static SnpEvolution.Tests.TestNetworks;

namespace SnpEvolution.Tests.Simulation
{
    public class NetworkSimulationTests
    {
        [Fact]
        public void OutputIsTheStepCountBetweenTheOutputNeuronsFirstAndSecondSpike()
        {
            var simulation = new NetworkSimulation(AlwaysOutputsOne(), new Random(0));

            simulation.Step();
            Assert.Null(simulation.Output);
            Assert.Equal(new long[] { 1, 1 }, simulation.Spikes);

            simulation.Step();
            Assert.Equal(1, simulation.Output);
            Assert.Equal(2, simulation.StepCount);
        }

        [Fact]
        public void OutputCountsTheSilentStepsBeforeTheSecondSpike()
        {
            var network = new Network(new[]
            {
                Neuron(1, new[] { 2 }, new Rule("a", 0, true)),
                Neuron(0, new[] { 3 }, new Rule("a", 0, true)),
                OutputNeuron(1, new Rule("a", 0, true)),
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
                Neuron(1, new[] { 3 }, new Rule("a", 0, true)),
                Neuron(1, new[] { 3 }, new Rule("a", 0, true)),
                Neuron(0, Array.Empty<int>(), new Rule("aaaa", 0, true)),
            });
            var simulation = new NetworkSimulation(network, new Random(0));

            simulation.Step();

            Assert.Equal(new long[] { 0, 0, 2 }, simulation.Spikes);
        }

        [Fact]
        public void RuleIsChosenOnlyAmongRulesThatMatch()
        {
            var network = new Network(new[]
            {
                Neuron(1, new[] { 2 }, new Rule("aaa", 0, false), new Rule("a", 0, true), new Rule("a", 0, true)),
                Neuron(0, Array.Empty<int>(), new Rule("aaaa", 0, true)),
            });

            for (int seed = 0; seed < 50; seed++)
            {
                var simulation = new NetworkSimulation(network, new Random(seed));
                simulation.Step();
                Assert.Equal(1, simulation.Spikes[1]);
            }
        }

        [Fact]
        public void ForgettingRuleConsumesSpikesWithoutEmitting()
        {
            var network = new Network(new[]
            {
                Neuron(2, new[] { 2 }, new Rule("aa", 0, false)),
                Neuron(0, Array.Empty<int>(), new Rule("aaaa", 0, true)),
            });
            var simulation = new NetworkSimulation(network, new Random(0));

            simulation.Step();

            Assert.Equal(new long[] { 0, 0 }, simulation.Spikes);
        }

        [Fact]
        public void DelayedRuleHoldsSpikesUntilTheDelayElapses()
        {
            var network = new Network(new[]
            {
                Neuron(1, Array.Empty<int>(), new Rule("a", 2, true)),
            });
            var simulation = new NetworkSimulation(network, new Random(0));

            simulation.Step();
            simulation.Step();
            Assert.Equal(1, simulation.Spikes[0]);

            simulation.Step();
            Assert.Equal(1, simulation.Spikes[0]);

            simulation.Step();
            Assert.Equal(0, simulation.Spikes[0]);
        }
    }
}
