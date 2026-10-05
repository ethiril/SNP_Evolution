using SnpEvolution.Networks;

namespace SnpEvolution.Tests
{
    internal static class TestNetworks
    {
        public static Neuron Neuron(long initialSpikes, int[] connections, params Rule[] rules) =>
            new Neuron(rules, initialSpikes, connections, false);

        public static Neuron OutputNeuron(long initialSpikes, params Rule[] rules) =>
            new Neuron(rules, initialSpikes, Array.Empty<int>(), true);

        // The output neuron fires at step one, a delayed feeder refills it, and it fires again at step two: always outputs 1.
        public static Neuron InputNeuron(int[] connections, params Rule[] rules) =>
            new Neuron(rules, 0, connections, false, isInput: true);

        // E/a^c -> a^p;d
        public static Rule Standard(string expression, long consume, int produce = 1, int delay = 0) =>
            new Rule(expression, delay, true, consume, produce);

        public static Rule StandardForget(string expression, long consume) => new Rule(expression, 0, false, consume);

        // Passes the number on its input to its output unchanged: the input relays each spike to the output, which fires on each.
        public static Network Identity() => new Network(new[]
        {
            InputNeuron(new[] { 2 }, Standard("a", 1)),
            new Neuron(new[] { Standard("a", 1) }, 0, Array.Empty<int>(), true),
        });

        public static Network AlwaysOutputsOne() => new Network(new[]
        {
            Neuron(1, new[] { 2 }, new Rule("a", 1, true)),
            OutputNeuron(1, new Rule("a", 0, true)),
        });

        // Two neurons pass one spike back and forth, so the output fires on steps 1, 3, 5, ...: every gap is 2.
        public static Network PingPong() => new Network(new[]
        {
            Neuron(1, new[] { 2 }, Standard("a", 1)),
            new Neuron(new[] { Standard("a", 1) }, 0, new[] { 1 }, true),
        });

        public static Network NeverOutputs() => new Network(new[]
        {
            OutputNeuron(1, new Rule("a", 0, false)),
        });
    }
}
