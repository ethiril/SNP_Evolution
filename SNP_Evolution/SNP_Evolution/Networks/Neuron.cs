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
            bool isOutput,
            bool isInput = false)
        {
            Rules = rules;
            InitialSpikes = initialSpikes;
            Connections = connections;
            IsOutput = isOutput;
            IsInput = isInput;
        }

        public IReadOnlyList<Rule> Rules { get; }

        [JsonProperty("SpikeCount"), JsonConverter(typeof(SpikeCountJsonConverter))]
        public long InitialSpikes { get; }

        // 1-based positions of the target neurons within the network.
        public IReadOnlyList<int> Connections { get; }

        public bool IsOutput { get; }

        // Input neurons also receive spikes from the environment, which is how a task feeds a network its arguments.
        public bool IsInput { get; }

        public Neuron WithRules(IEnumerable<Rule> rules) => new Neuron(rules.ToList(), InitialSpikes, Connections, IsOutput, IsInput);

        public Neuron WithInitialSpikes(long initialSpikes) => new Neuron(Rules, initialSpikes, Connections, IsOutput, IsInput);

        public Neuron WithConnections(IEnumerable<int> connections) =>
            new Neuron(Rules, InitialSpikes, connections.Distinct().OrderBy(position => position).ToList(), IsOutput, IsInput);

        public Neuron WithRoles(bool isOutput, bool isInput) => new Neuron(Rules, InitialSpikes, Connections, isOutput, isInput);
    }
}
