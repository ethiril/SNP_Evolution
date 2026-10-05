using SnpEvolution.Networks;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Storage
{
    public class NetworkFilesTests
    {
        private const string LegacyJson = @"{
            ""Neurons"": [ {
                ""Rules"": [ { ""RuleExpression"": ""aa"", ""Delay"": 1, ""Fire"": true } ],
                ""SpikeCount"": ""aa"", ""Connections"": [ 1 ], ""ActiveDelay"": 0, ""PersistedState"": false, ""IsOutput"": true
            } ],
            ""OutputSet"": [], ""CurrentOutput"": 0, ""IsClear"": false, ""GlobalTimer"": 0, ""IsEngaged"": false
        }";

        [Fact]
        public void LoadsNetworksSavedByThePreviousVersion()
        {
            Network? network = NetworkFiles.FromJson(LegacyJson);

            Neuron neuron = Assert.Single(Assert.IsType<Network>(network).Neurons);
            Assert.Equal(2, neuron.InitialSpikes);
            Assert.Equal(new[] { 1 }, neuron.Connections);
            Assert.True(neuron.IsOutput);
            Rule rule = Assert.Single(neuron.Rules);
            Assert.Equal(("aa", 1, true), (rule.Expression, rule.Delay, rule.Fire));
        }

        [Fact]
        public void RoundTripsThroughJson()
        {
            Network original = ReferenceNetworks.EvenNumbers();

            string json = NetworkFiles.ToJson(original);

            Assert.Equal(json, NetworkFiles.ToJson(Assert.IsType<Network>(NetworkFiles.FromJson(json))));
            Assert.Contains("\"SpikeCount\"", json);
            Assert.Contains("\"RuleExpression\"", json);
        }

        [Fact]
        public void SavesSpikeCountsAsNumbers()
        {
            string json = NetworkFiles.ToJson(new Network(new[] { TestNetworks.OutputNeuron(5_000_000_000, new Rule("a+", 0, true)) }));

            Assert.Contains("\"SpikeCount\": 5000000000", json);
            Assert.Equal(5_000_000_000, NetworkFiles.FromJson(json)?.Neurons[0].InitialSpikes);
        }

        [Theory]
        [InlineData("\"ab\"")]
        [InlineData("-1")]
        public void RejectsAnInvalidSpikeCount(string spikeCount)
        {
            Assert.Null(NetworkFiles.FromJson(LegacyJson.Replace("\"SpikeCount\": \"aa\"", "\"SpikeCount\": " + spikeCount)));
        }

        [Fact]
        public void RejectsAnInvalidRuleExpression()
        {
            Assert.Null(NetworkFiles.FromJson(LegacyJson.Replace("\"RuleExpression\": \"aa\"", "\"RuleExpression\": \"(\"")));
        }

        [Fact]
        public void RejectsAMissingFile()
        {
            Assert.Null(NetworkFiles.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")));
        }
    }
}
