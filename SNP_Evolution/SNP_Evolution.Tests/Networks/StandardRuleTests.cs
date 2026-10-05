using SnpEvolution.Networks;
using SnpEvolution.Storage;
using static SnpEvolution.Tests.TestNetworks;

namespace SnpEvolution.Tests.Networks
{
    public class StandardRuleTests
    {
        [Theory]
        [InlineData("a(aa)*", 2, 3, true)]
        [InlineData("a(aa)*", 2, 1, false)]
        [InlineData("a(aa)*", 2, 4, false)]
        [InlineData("a+", 3, 3, true)]
        public void AppliesOnlyWhenTheExpressionMatchesAndEnoughSpikesAreThere(string expression, long consume, long spikes, bool applies)
        {
            Assert.Equal(applies, Standard(expression, consume).Applies(spikes));
        }

        [Fact]
        public void LegacyRulesAreNotStandardAndProduceOneSpike()
        {
            var rule = new Rule("aa", 0, true);

            Assert.False(rule.IsStandard);
            Assert.Null(rule.Consume);
            Assert.Equal(1, rule.Produce);
        }

        [Theory]
        [InlineData("a(aa)*", 1L, 1, 0, true, "a(aa)*/a -> a")]
        [InlineData("aaa", 3L, 2, 0, true, "aaa -> aa")]
        [InlineData("a+", 2L, 1, 2, true, "a+/aa -> a;2")]
        [InlineData("aa", 2L, 1, 0, false, "aa -> forget")]
        public void IsWrittenInSnpNotation(string expression, long consume, int produce, int delay, bool fire, string notation)
        {
            Assert.Equal(notation, NetworkNotation.Rule(new Rule(expression, delay, fire, consume, produce)));
        }

        [Fact]
        public void TableMarksInputNeurons()
        {
            Assert.StartsWith("n1 (in)", NetworkNotation.Format(Identity()).Split(Environment.NewLine)[1]);
        }

        [Fact]
        public void StandardRulesAndInputNeuronsRoundTripThroughJson()
        {
            var network = new Network(new[]
            {
                InputNeuron(new[] { 2 }, Standard("a(aa)*", 1, produce: 2, delay: 1)),
                OutputNeuron(0, new Rule("a", 0, true)),
            });

            string json = NetworkFiles.ToJson(network);
            Network loaded = Assert.IsType<Network>(NetworkFiles.FromJson(json));

            Rule rule = loaded.Neurons[0].Rules[0];
            Assert.Equal((1L, 2, 1), (rule.Consume!.Value, rule.Produce, rule.Delay));
            Assert.True(loaded.Neurons[0].IsInput);
            Assert.Null(loaded.Neurons[1].Rules[0].Consume);
            Assert.DoesNotContain("\"Size\"", json);
            Assert.Equal(json, NetworkFiles.ToJson(loaded));
        }

        [Fact]
        public void SizeCountsNeuronsRulesAndSynapses()
        {
            Network network = ReferenceNetworks.NaturalNumbers();

            Assert.Equal((4, 8, 9), (network.Neurons.Count, network.RuleCount, network.SynapseCount));
            Assert.Equal(400 + 80 + 9, network.Size);
        }
    }
}
