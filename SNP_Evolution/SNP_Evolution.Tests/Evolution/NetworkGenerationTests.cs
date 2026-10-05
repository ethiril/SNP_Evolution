using System.Text.RegularExpressions;
using SnpEvolution.Evolution;
using SnpEvolution.Networks;

namespace SnpEvolution.Tests.Evolution
{
    public class NetworkGenerationTests
    {
        [Fact]
        public void ExpressionsFillBothPlaceholdersWithShortSpikeRuns()
        {
            var generator = new ExpressionGenerator(new[] { "x(y)+" }, maxSpikeGroupSize: 4, new Random(0));

            for (int sample = 0; sample < 50; sample++)
            {
                Assert.Matches(new Regex(@"^a{1,3}\(a{1,3}\)\+$"), generator.Next());
            }
        }

        [Fact]
        public void EachPlaceholderGetsItsOwnSpikeRun()
        {
            var generator = new ExpressionGenerator(new[] { "x,y" }, maxSpikeGroupSize: 4, new Random(0));

            bool anyDiffer = Enumerable.Range(0, 50).Select(_ => generator.Next().Split(',')).Any(parts => parts[0] != parts[1]);

            Assert.True(anyDiffer);
        }

        [Fact]
        public void RandomTopologyHasOneOutputAndValidConnections()
        {
            for (int seed = 0; seed < 100; seed++)
            {
                var random = new Random(seed);
                Network network = RandomTopology.Create(new ExpressionGenerator(ExpressionGenerator.SimpleTemplates, 4, random), 4, random);

                Assert.Single(network.Neurons, neuron => neuron.IsOutput);
                for (int position = 1; position <= network.Neurons.Count; position++)
                {
                    IReadOnlyList<int> connections = network.Neurons[position - 1].Connections;
                    Assert.NotEmpty(connections);
                    Assert.DoesNotContain(position, connections);
                    Assert.All(connections, target => Assert.InRange(target, 1, network.Neurons.Count));
                    Assert.Equal(connections.Distinct().OrderBy(target => target), connections);
                }
            }
        }

        [Fact]
        public void RandomExpressionsKeepEachRulesDelayAndFiring()
        {
            Network reference = ReferenceNetworks.NaturalNumbers();

            Network randomised = reference.WithRandomExpressions(() => "aaaa");

            Assert.All(randomised.Neurons.SelectMany(neuron => neuron.Rules), rule => Assert.Equal("aaaa", rule.Expression));
            Assert.Equal(
                reference.Neurons.SelectMany(neuron => neuron.Rules).Select(rule => (rule.Delay, rule.Fire)),
                randomised.Neurons.SelectMany(neuron => neuron.Rules).Select(rule => (rule.Delay, rule.Fire)));
        }
    }
}
