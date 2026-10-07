using SnpEvolution.Model;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Model
{
    public class HardwareProfileTests
    {
        [Fact]
        public void RejectsAParityConditionAndNamesTheRule()
        {
            var network = new Network(new[]
            {
                Neuron(0, new[] { 2 }, HardwareProfile.ThresholdRule(2)),
                OutputNeuron(0, new Rule("a(aa)*", 0, true)),
            });

            string problem = Assert.Single(HardwareProfile.Problems(network));

            Assert.StartsWith("Neuron 2, rule 1 (a(aa)* -> a):", problem);
            Assert.Contains("accepts 1, 3, 5, 7, 9, ...", problem);
        }

        [Fact]
        public void NamesEveryOtherWayOutOfTheProfile()
        {
            var network = new Network(new[]
            {
                Neuron(2, new[] { 2 }, Standard("a+", 1, produce: 2), new Rule("aa+", 2, true)),
            });

            IReadOnlyList<string> problems = HardwareProfile.Problems(network);

            Assert.Contains(problems, problem => problem.Contains("starts with 2 spike(s)"));
            Assert.Contains(problems, problem => problem.Contains("has 2 rules"));
            Assert.Contains(problems, problem => problem.Contains("rule 1 (a+/a -> aa): it consumes 1 spike(s)"));
            Assert.Contains(problems, problem => problem.Contains("rule 1 (a+/a -> aa): it sends 2 spikes"));
            Assert.Contains(problems, problem => problem.Contains("rule 2 (aa+ -> a;2): its delay of 2 holds the neuron"));
        }

        [Theory]
        [InlineData("a+", 1)]
        [InlineData("aa+", 2)]
        [InlineData("a{3,}", 3)]
        [InlineData("aaa*", 2)]
        [InlineData("(aa|aaa)a*", 2)]
        [InlineData("a*", 1)]
        [InlineData("a(aa)*", null)]
        [InlineData("aaa", null)]
        [InlineData("b", null)]
        public void ReadsTheThresholdOfAThresholdCondition(string expression, int? threshold)
        {
            Assert.Equal(threshold, HardwareProfile.Threshold(SpikeCondition.Parse(expression)));
        }

        [Fact]
        public void ConformsToTheNearestThresholdRule()
        {
            Rule conformed = HardwareProfile.Conform(Standard("a(aa)+", 2, delay: 2));
            Assert.Equal(("a{3,}", 2, true, true, false), (conformed.Expression, conformed.Delay, conformed.Fire, conformed.Axonal, conformed.IsStandard));

            Rule forgetting = HardwareProfile.Conform(new Rule("aa", 1, false));
            Assert.Equal(("a{2,}", 0, false, false), (forgetting.Expression, forgetting.Delay, forgetting.Fire, forgetting.Axonal));

            Neuron neuron = HardwareProfile.Conform(Neuron(3, new[] { 2 }, new Rule("aa+", 0, true), new Rule("a", 0, true)));
            Assert.Equal(0, neuron.InitialSpikes);
            Assert.Single(neuron.Rules);
        }

        [Fact]
        public void LeavesAFittingNetworkAsItIs()
        {
            var network = new Network(new[] { Neuron(0, new[] { 2 }, HardwareProfile.ThresholdRule(1, delay: 2)), OutputNeuron(0, HardwareProfile.ThresholdRule(3, fire: false)) });

            Assert.Same(network, HardwareProfile.Conform(network));
        }
    }
}
