using SnpEvolution.Model;
using SnpEvolution.Simulation;

namespace SnpEvolution.Tests.Simulation
{
    public class NetworkRunnerTests
    {
        private sealed class CountingRandom : Random
        {
            public int Calls { get; private set; }

            public override int Next(int maxValue)
            {
                Calls++;
                return 0;
            }
        }

        [Fact]
        public void CollectsOneOutputPerSuccessfulRun()
        {
            List<int> outputs = NetworkRunner.CollectOutputs(TestNetworks.AlwaysOutputsOne(), maxSteps: 10, repetitions: 12, new Random(0));

            Assert.Equal(Enumerable.Repeat(1, 12), outputs);
        }

        [Fact]
        public void GivesUpOnANetworkThatStaysSilentForSevenRuns()
        {
            var network = new Network(new[]
            {
                TestNetworks.OutputNeuron(1, new Rule("a", 0, false), new Rule("a", 0, false)),
            });
            var random = new CountingRandom();

            List<int> outputs = NetworkRunner.CollectOutputs(network, maxSteps: 5, repetitions: 50, random);

            Assert.Empty(outputs);
            Assert.Equal(7, random.Calls);
        }

        [Fact]
        public void StopsARunAtMaxSteps()
        {
            Assert.Null(NetworkRunner.RunOnce(TestNetworks.AlwaysOutputsOne(), maxSteps: 1, new Random(0)));
        }

        [Fact]
        public void ReferenceNetworksProduceSortedPositiveOutputs()
        {
            foreach (Network network in new[] { ReferenceNetworks.NaturalNumbers(), ReferenceNetworks.EvenNumbers() })
            {
                List<int> outputs = NetworkRunner.CollectOutputs(network, maxSteps: 50, repetitions: 50, new Random(3));

                Assert.NotEmpty(outputs);
                Assert.All(outputs, output => Assert.True(output > 0));
                Assert.Equal(outputs.OrderBy(output => output), outputs);
            }
        }
    }
}
