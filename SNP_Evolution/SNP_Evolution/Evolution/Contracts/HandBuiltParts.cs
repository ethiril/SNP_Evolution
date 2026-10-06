using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Contracts
{
    // Hand-built parts with count ports, which evolve-parts has not found yet. They only ever enter a library when
    // asked for, since the default is a library the run discovers itself. Each is laid out start, data in-ports, data
    // out-ports, done ports, then its own neurons, so PortBinding.AfterInputs binds it.
    public static class HandBuiltParts
    {
        public const string Origin = "hand-built";

        // The hand-built register, add and increment, checked against the catalogue's contracts, and the parts below.
        public static IReadOnlyList<Part> All() => new[]
        {
            ReferenceParts.Register(FirstParts.Larger) with { Contract = FirstParts.Named("register") },
            ReferenceParts.Add(),
            ReferenceParts.Increment(),
            FanOut(),
            ZeroTest(),
            Decrement(),
            Gate(),
        };

        // A register draining into two outputs at once.
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
            return new Part(contract, new Network(neurons), PortBinding.AfterInputs(contract));
        }

        // Each input spike is stored as four and start adds one, so the store holds 1 when n is 0 and 4n + 1 otherwise.
        // It fires once, one spike or two, which picks the branch, and forgets the rest four at a time.
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
            return new Part(contract, new Network(neurons), PortBinding.AfterInputs(contract));
        }

        // A register whose start also reaches out a step late through a relay, landing on the store's first spike so out drops that pair.
        public static Part Decrement()
        {
            Contract contract = ArithmeticParts.Decrement();
            const int Out = 3, Done = 4, Store = 5, Relay = 6;
            var neurons = new List<Neuron>
            {
                new Neuron(new[] { Rule.Standard("a", 1) }, 0, new[] { Store, Relay }, false, isInput: true),
                new Neuron(new[] { Rule.Standard("a", 1, 2) }, 0, new[] { Store }, false, isInput: true),
                CountOut(),
                DoneOnAPair(),
                new Neuron(new[] { Rule.Standard("a(aa)+", 2), Rule.Standard("a", 1, 2) }, 0, new[] { Out, Done }, false),
                new Neuron(new[] { Rule.Standard("a", 1) }, 0, new[] { Out }, false),
            };
            return new Part(contract, new Network(neurons), PortBinding.AfterInputs(contract));
        }

        // n is stored four to a spike and open adds two, so after start's one the store is 3 more than a multiple of four
        // when open and 1 more when shut. Open, it drains a spike a step to out as a register does; shut, it forgets four a step.
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
            return new Part(contract, new Network(neurons), PortBinding.AfterInputs(contract));
        }

        // Fires on a single spike and drops a pair, the store's sign that it is empty.
        private static Neuron CountOut() => new Neuron(new[] { Rule.Standard("a", 1), Rule.Forget("aa", 2) }, 0, new int[0], false);

        private static Neuron DoneOnAPair() => new Neuron(new[] { Rule.Forget("a", 1), Rule.Standard("aa", 2) }, 0, new int[0], false);
    }
}
