using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Contracts
{
    // A network with the contract it meets and the neurons that carry its ports.
    public sealed record Part(Contract Contract, Network Network, PortBinding Binding)
    {
        public ContractTask Task() => new ContractTask(Contract, Binding);
    }

    // Hand-built parts, each with a copy broken in exactly one rule, that show ContractTask accepts a correct part
    // and rejects a broken one before evolution is trusted with it. All are built from standard rules and bound to the
    // neurons straight after the inputs, as evolved parts are.
    public static class ReferenceParts
    {
        public static Contract DelayContract(int k) => new Contract(
            $"delay {k}",
            Port.In("start", PortKind.Trigger),
            new[] { Port.Out("done", PortKind.Trigger) },
            new Port[0],
            new[] { new ContractCase(new Dictionary<string, int>(), new Dictionary<string, int>(), "done") },
            MaxLatency: k,
            MinLatency: k);

        // Done fires k steps after start reaches the part: start relays the spike to done, which holds it for k - 1
        // steps before firing.
        public static Part Delay(int k) => DelayPart(k, startProduces: 1);

        // Start sends done two spikes instead of one, so done fires on time and then again.
        public static Part DelayFiringDoneTwice(int k) => DelayPart(k, startProduces: 2);

        public static Contract RegisterContract(int largest = 8) => new Contract(
            "register",
            Port.In("start", PortKind.Trigger),
            new[] { Port.Out("done", PortKind.Trigger) },
            new[] { Port.In("n", PortKind.Count), Port.Out("out", PortKind.Count) },
            Enumerable.Range(0, largest + 1)
                .Select(n => new ContractCase(new Dictionary<string, int> { ["n"] = n }, new Dictionary<string, int> { ["out"] = n }, "done"))
                .ToList(),
            MaxLatency: largest + 2);

        // Holds the count loaded on n until started, then drains it to out one spike per step and fires done. The
        // store neuron holds 2n, not n: each spike on n arrives doubled, start adds one, and the store drains two at a
        // time while it holds an odd number of at least three. When one is left it sends two spikes, which out
        // forgets and done fires on. Parity is how Ionescu, Paun and Yokomori (2006) test for zero; the count ports
        // still carry n.
        public static Part Register(int largest = 8) => RegisterPart(largest, leaveSpike: false);

        // Start also feeds a sixth neuron with no rules, which keeps the spike after done.
        public static Part RegisterLeavingASpike(int largest = 8) => RegisterPart(largest, leaveSpike: true);

        private static Part DelayPart(int k, int startProduces)
        {
            Contract contract = DelayContract(k);
            var network = new Network(new[]
            {
                new Neuron(new[] { Standard("a", 1, startProduces) }, 0, new[] { 2 }, false, isInput: true),
                new Neuron(new[] { Standard("a+", 1, 1, k - 1) }, 0, new int[0], false),
            });
            return new Part(contract, network, PortBinding.AfterInputs(contract));
        }

        private static Part RegisterPart(int largest, bool leaveSpike)
        {
            Contract contract = RegisterContract(largest);
            const int Out = 3, Done = 4, Store = 5, Sink = 6;
            var neurons = new List<Neuron>
            {
                new Neuron(new[] { Standard("a", 1) }, 0, leaveSpike ? new[] { Store, Sink } : new[] { Store }, false, isInput: true),
                new Neuron(new[] { Standard("a", 1, 2) }, 0, new[] { Store }, false, isInput: true),
                new Neuron(new[] { Standard("a", 1), Forget("aa", 2) }, 0, new int[0], false),
                new Neuron(new[] { Forget("a", 1), Standard("aa", 2) }, 0, new int[0], false),
                new Neuron(new[] { Standard("a(aa)+", 2), Standard("a", 1, 2) }, 0, new[] { Out, Done }, false),
            };
            if (leaveSpike)
            {
                neurons.Add(new Neuron(new Rule[0], 0, new int[0], false));
            }
            return new Part(contract, new Network(neurons), PortBinding.AfterInputs(contract));
        }

        // E/a^c -> a^p;d
        private static Rule Standard(string expression, long consume, int produce = 1, int delay = 0) => new Rule(expression, delay, true, consume, produce);

        private static Rule Forget(string expression, long consume) => new Rule(expression, 0, false, consume);
    }
}
