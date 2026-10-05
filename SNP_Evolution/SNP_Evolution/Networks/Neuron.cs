using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace SnpEvolution.Networks
{
    public sealed class Neuron
    {
        public Neuron(IReadOnlyList<Rule> rules, [JsonProperty("SpikeCount")] string initialSpikes, IReadOnlyList<int> connections, bool isOutput)
        {
            Rules = rules;
            InitialSpikes = initialSpikes;
            Connections = connections;
            IsOutput = isOutput;
        }

        public IReadOnlyList<Rule> Rules { get; }

        // One 'a' per spike, so rule expressions can match it directly.
        [JsonProperty("SpikeCount")]
        public string InitialSpikes { get; }

        // 1-based positions of the target neurons within the network.
        public IReadOnlyList<int> Connections { get; }

        public bool IsOutput { get; }

        public Neuron WithRules(IEnumerable<Rule> rules) => new Neuron(rules.ToList(), InitialSpikes, Connections, IsOutput);
    }
}
