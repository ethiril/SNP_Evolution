using SnpEvolution.Networks;

namespace SnpEvolution.Tests
{
    internal static class TestNetworks
    {
        public static Neuron Neuron(string initialSpikes, int[] connections, params Rule[] rules) =>
            new Neuron(rules, initialSpikes, connections, false);

        public static Neuron OutputNeuron(string initialSpikes, params Rule[] rules) =>
            new Neuron(rules, initialSpikes, Array.Empty<int>(), true);

        // The output neuron fires at step one, a delayed feeder refills it, and it fires again at step two: always outputs 1.
        public static Network AlwaysOutputsOne() => new Network(new[]
        {
            Neuron("a", new[] { 2 }, new Rule("a", 1, true)),
            OutputNeuron("a", new Rule("a", 0, true)),
        });

        public static Network NeverOutputs() => new Network(new[]
        {
            OutputNeuron("a", new Rule("a", 0, false)),
        });
    }
}
