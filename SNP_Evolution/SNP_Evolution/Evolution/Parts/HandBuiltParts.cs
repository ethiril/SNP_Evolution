using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Networks;
using static SnpEvolution.Evolution.Parts.ReferenceParts;

namespace SnpEvolution.Evolution.Parts
{
    // Each is laid out start, data in-ports, data out-ports, done ports, then its own neurons, so PortLayout.AfterInputs binds it.
    public static class HandBuiltParts
    {
        public const string Origin = "hand-built";

        public static IReadOnlyList<Part> All() => new[]
        {
            ReferenceParts.Register(),
            ReferenceParts.Add(),
            ReferenceParts.Increment(),
            FanOut(),
            ZeroTest(),
            Decrement(),
            Gate(),
        };

        public static Part FanOut()
        {
            Contract contract = FirstParts.Named("fan-out");
            const int A = 3, B = 4, Done = 5, Store = 6;
            var neurons = new List<Neuron>
            {
                new Neuron(new[] { Rule.Standard("a", 1) }, 0, new[] { Store }, false, isInput: true),
                new Neuron(new[] { Rule.Standard("a", 1, 2) }, 0, new[] { Store }, false, isInput: true),
                CountOut(),
                CountOut(),
                DoneOnAPair(),
                new Neuron(new[] { Rule.Standard("a(aa)+", 2), Rule.Standard("a", 1, 2) }, 0, new[] { A, B, Done }, false),
            };
            return new Part(contract, new Network(neurons), PortLayout.AfterInputs(contract));
        }

        // Each input spike is stored as four and start adds one, so 1 left after firing means zero and 4n + 1 means not.
        public static Part ZeroTest()
        {
            Contract contract = FirstParts.Named("zero test");
            const int Zero = 3, Nonzero = 4, Store = 5;
            var neurons = new List<Neuron>
            {
                new Neuron(new[] { Rule.Standard("a", 1) }, 0, new[] { Store }, false, isInput: true),
                new Neuron(new[] { Rule.Standard("a", 1, 4) }, 0, new[] { Store }, false, isInput: true),
                new Neuron(new[] { Rule.Standard("a", 1), Rule.Forget("aa", 2) }, 0, new int[0], false),
                new Neuron(new[] { Rule.Forget("a", 1), Rule.Standard("aa", 2) }, 0, new int[0], false),
                new Neuron(new[] { Rule.Standard("a", 1), Rule.Standard("a(aaaa)+", 3, 2), Rule.Forget("aa(aaaa)+", 4), Rule.Forget("aa", 2) }, 0,
                    new[] { Zero, Nonzero }, false),
            };
            return new Part(contract, new Network(neurons), PortLayout.AfterInputs(contract));
        }

        // A register whose start also reaches out a step late through a relay, landing on the store's first spike so out drops that pair.
        public static Part Decrement()
        {
            Contract contract = ArithmeticParts.Decrement();
            const int Out = 3, Store = 5, Relay = 6;
            List<Neuron> neurons = DrainingStore(countInputs: 1, startTargets: new[] { Store, Relay });
            neurons.Add(new Neuron(new[] { Rule.Standard("a", 1) }, 0, new[] { Out }, false));
            return new Part(contract, new Network(neurons), PortLayout.AfterInputs(contract));
        }

        // n is stored four to a spike and open adds two, so the store's remainder mod four says open (3) or shut (1).
        public static Part Gate()
        {
            Contract contract = ArithmeticParts.Gate();
            const int Out = 4, Done = 5, Store = 6;
            var neurons = new List<Neuron>
            {
                new Neuron(new[] { Rule.Standard("a", 1) }, 0, new[] { Store }, false, isInput: true),
                new Neuron(new[] { Rule.Standard("a", 1, 4) }, 0, new[] { Store }, false, isInput: true),
                new Neuron(new[] { Rule.Standard("a", 1, 2) }, 0, new[] { Store }, false, isInput: true),
                CountOut(),
                DoneOnAPair(),
                new Neuron(new[]
                {
                    Rule.Standard("aaa(aaaa)+", 4), Rule.Standard("aaa", 3, 2),
                    Rule.Forget("a(aaaa)+", 4), Rule.Standard("a", 1, 2),
                }, 0, new[] { Out, Done }, false),
            };
            return new Part(contract, new Network(neurons), PortLayout.AfterInputs(contract));
        }
    }
}
