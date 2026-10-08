using SnpEvolution.Model;

namespace SnpEvolution.Tests.Fixtures
{
    // Hand-wired networks for the trigger-port control contracts, laid out as their bindings read them.
    public static class ControlNetworks
    {
        // Start, then a and b, relayed to done, which fires on the pair; with relay true it fires on each spike instead.
        public static Network JoinOnAPair(bool relay) => new Network(new[]
        {
            new Neuron(new[] { Rule.Standard("a", 1) }, 0, new int[0], false, isInput: true),
            new Neuron(new[] { Rule.Standard("a", 1) }, 0, new[] { 4 }, false, isInput: true),
            new Neuron(new[] { Rule.Standard("a", 1) }, 0, new[] { 4 }, false, isInput: true),
            new Neuron(new[] { relay ? Rule.Standard("a", 1) : Rule.Standard("aa", 2) }, 0, new int[0], false),
        });

        // start -> t1 and t2 -> done; with apart true, t2 hangs off t1 and fires a step later.
        public static Network Fork(bool apart) => new Network(new[]
        {
            new Neuron(new[] { Rule.Standard("a", 1) }, 0, apart ? new[] { 2 } : new[] { 2, 3 }, false, isInput: true),
            new Neuron(new[] { Rule.Standard("a", 1) }, 0, apart ? new[] { 3, 4 } : new[] { 4 }, false),
            new Neuron(new[] { Rule.Standard("a", 1) }, 0, apart ? new[] { 4 } : new int[0], false),
            new Neuron(new[] { Rule.Standard("a", 1), Rule.Forget("aa", 2) }, 0, new int[0], false),
        });
    }
}
