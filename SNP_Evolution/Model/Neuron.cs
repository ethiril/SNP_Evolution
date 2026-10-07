using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace SnpEvolution.Model
{
    // Marks a neuron as part of a copy of a library module: which module, and which copy of it in the network.
    public sealed record ModuleTag(int Module, int Instance);

    public sealed class Neuron
    {
        public Neuron(
            IReadOnlyList<Rule> rules,
            [JsonProperty("SpikeCount")] long initialSpikes,
            IReadOnlyList<int> connections,
            bool isOutput,
            bool isInput = false,
            ModuleTag? module = null)
        {
            Module = module;
            Rules = rules;
            InitialSpikes = initialSpikes;
            Connections = connections;
            IsOutput = isOutput;
            IsInput = isInput;
        }

        public IReadOnlyList<Rule> Rules { get; }

        [JsonProperty("SpikeCount")]
        public long InitialSpikes { get; }

        // 1-based positions of the target neurons within the network.
        public IReadOnlyList<int> Connections { get; }

        public bool IsOutput { get; }

        // Input neurons also receive spikes from the environment, which is how a task feeds a network its arguments.
        public bool IsInput { get; }

        // Set when the neuron came from a library module and still belongs to it; the simulation ignores it.
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public ModuleTag? Module { get; }

        public Neuron WithRules(IEnumerable<Rule> rules) => new Neuron(rules.ToList(), InitialSpikes, Connections, IsOutput, IsInput, Module);

        public Neuron WithInitialSpikes(long initialSpikes) => new Neuron(Rules, initialSpikes, Connections, IsOutput, IsInput, Module);

        public Neuron WithConnections(IEnumerable<int> connections) =>
            new Neuron(Rules, InitialSpikes, connections.Distinct().OrderBy(position => position).ToList(), IsOutput, IsInput, Module);

        public Neuron WithRoles(bool isOutput, bool isInput) => new Neuron(Rules, InitialSpikes, Connections, isOutput, isInput, Module);

        public Neuron WithModule(ModuleTag? module) => new Neuron(Rules, InitialSpikes, Connections, IsOutput, IsInput, module);
    }
}
