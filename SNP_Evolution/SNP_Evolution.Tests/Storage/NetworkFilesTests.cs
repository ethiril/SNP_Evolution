using SnpEvolution.Model;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Storage;
using static SnpEvolution.Tests.Fixtures.ModuleFixtures;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

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
            string json = NetworkFiles.ToJson(new Network(new[] { OutputNeuron(5_000_000_000, new Rule("a+", 0, true)) }));

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

        [Fact]
        public void ModuleTagsSurviveSavingAndAreShownInTheNotation()
        {
            var library = new ModuleLibrary();
            Network network = ModuleEdits.Insert(PingPong(), ModuleOf(library, Chain()), 3, 10, library, new Random(1));

            Network loaded = NetworkFiles.FromJson(NetworkFiles.ToJson(network))!;

            Assert.Equal(network.Neurons.Select(neuron => neuron.Module), loaded.Neurons.Select(neuron => neuron.Module));
            Assert.Contains("[module 1]", NetworkNotation.Format(network));
            Assert.DoesNotContain("Module", NetworkFiles.ToJson(PingPong()));
        }
    }
}
