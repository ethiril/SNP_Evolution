using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Model;
using SnpEvolution.Specs.Contracts;

namespace SnpEvolution.Specs.Parts
{
    // Hand-built parts for catalogue contracts, built and bound the way evolved parts are.
    public static class ReferenceParts
    {
        // Start relays the spike to done, which holds it for k - 1 steps.
        public static Part Delay(int k)
        {
            Contract contract = FirstParts.Named($"delay {k}");
            var network = new Network(new[]
            {
                new Neuron(new[] { Rule.Standard("a", 1) }, 0, new[] { 2 }, false, isInput: true),
                new Neuron(new[] { Rule.Standard("a+", 1, 1, k - 1) }, 0, new int[0], false),
            });
            return new Part(contract, network, PortLayout.AfterInputs(contract));
        }

        // The store holds 2n while the count ports carry n, because the odd spike start adds is what tells a draining store from a loaded one.
        public static Part Register()
        {
            Contract contract = FirstParts.Named("register");
            const int Store = 5;
            return new Part(contract, new Network(DrainingStore(countInputs: 1, startTargets: new[] { Store })), PortLayout.AfterInputs(contract));
        }

        // A register whose start spike also goes straight to out, so out carries one spike more than the store drains.
        public static Part Increment()
        {
            Contract contract = FirstParts.Named("increment");
            const int Out = 3, Store = 5;
            return new Part(contract, new Network(DrainingStore(countInputs: 1, startTargets: new[] { Out, Store })), PortLayout.AfterInputs(contract));
        }

        // A register with a second count in-port feeding the same store, so the sum drains as one count.
        public static Part Add()
        {
            Contract contract = FirstParts.Named("add");
            const int Store = 6;
            return new Part(contract, new Network(DrainingStore(countInputs: 2, startTargets: new[] { Store })), PortLayout.AfterInputs(contract));
        }

        // The positions callers bind ports to: start, then the count in-ports, out, done and the store.
        internal static List<Neuron> DrainingStore(int countInputs, int[] startTargets)
        {
            int outPosition = countInputs + 2, donePosition = countInputs + 3, storePosition = countInputs + 4;
            var neurons = new List<Neuron> { new Neuron(new[] { Rule.Standard("a", 1) }, 0, startTargets, false, isInput: true) };
            neurons.AddRange(Enumerable.Range(0, countInputs).Select(_ => new Neuron(new[] { Rule.Standard("a", 1, 2) }, 0, new[] { storePosition }, false, isInput: true)));
            neurons.Add(CountOut());
            neurons.Add(DoneOnAPair());
            neurons.Add(new Neuron(new[] { Rule.Standard("a(aa)+", 2), Rule.Standard("a", 1, 2) }, 0, new[] { outPosition, donePosition }, false));
            return neurons;
        }

        // Fires on a single spike and drops a pair, the store's sign that it is empty.
        internal static Neuron CountOut() => new Neuron(new[] { Rule.Standard("a", 1), Rule.Forget("aa", 2) }, 0, new int[0], false);

        internal static Neuron DoneOnAPair() => new Neuron(new[] { Rule.Forget("a", 1), Rule.Standard("aa", 2) }, 0, new int[0], false);
    }
}
