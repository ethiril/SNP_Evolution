using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SnpEvolution.Storage;

namespace SnpEvolution.Networks
{
    public sealed class Neuron
    {
        public Neuron(
            IReadOnlyList<Rule> rules,
            [JsonProperty("SpikeCount"), JsonConverter(typeof(SpikeCountJsonConverter))] long initialSpikes,
            IReadOnlyList<int> connections,
            bool isOutput)
        {
            Rules = rules;
            InitialSpikes = initialSpikes;
            Connections = connections;
            IsOutput = isOutput;
        }

        public IReadOnlyList<Rule> Rules { get; }

        [JsonProperty("SpikeCount"), JsonConverter(typeof(SpikeCountJsonConverter))]
        public long InitialSpikes { get; }

        // 1-based positions of the target neurons within the network.
        public IReadOnlyList<int> Connections { get; }

        public bool IsOutput { get; }

        public Neuron WithRules(IEnumerable<Rule> rules) => new Neuron(rules.ToList(), InitialSpikes, Connections, IsOutput);
    }
}
