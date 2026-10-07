using SnpEvolution.Model;

namespace SnpEvolution.Tests.Model
{
    public class NetworkTests
    {
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

        [Fact]
        public void SizeCountsNeuronsRulesAndSynapses()
        {
            const int neuronWeight = 100, ruleWeight = 10, synapseWeight = 1;
            Network network = ReferenceNetworks.NaturalNumbers();

            Assert.Equal((4, 8, 9), (network.Neurons.Count, network.RuleCount, network.SynapseCount));
            Assert.Equal((neuronWeight * 4) + (ruleWeight * 8) + (synapseWeight * 9), network.Size);
        }
    }
}
