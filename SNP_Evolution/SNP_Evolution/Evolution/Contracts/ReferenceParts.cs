using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Contracts
{
    public sealed record Part(Contract Contract, Network Network, PortBinding Binding)
    {
        public ContractTask Task() => new ContractTask(Contract, Binding);
    }

    // Where a library part came from: the seed its contract was evolved with, the run that found it, and the networks
    // scored to find, shrink and verify it.
    public sealed record PartOrigin(int Seed, string Run, long Evaluations);

    // A verified part as the library keeps it, with what measuring it on its contract gave.
    public sealed record LibraryPart(Part Part, HardwareCost Cost, int Latency, string Behaviour, PartOrigin Origin)
    {
        public Contract Contract => Part.Contract;

        public static LibraryPart Of(Part part, PartMeasurement measurement, PartOrigin origin) =>
            new LibraryPart(part, measurement.Cost, measurement.Latency, measurement.Behaviour, origin);
    }

    // Hand-built parts, each with a copy broken in exactly one contract rule, built and bound the way evolved parts are.
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

        // Start relays the spike to done, which holds it for k - 1 steps.
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

        // The store holds 2n while the count ports carry n, because the odd spike start adds is what tells a draining store from a loaded one.
        public static Part Register(int largest = 8) => RegisterPart(largest, leaveSpike: false);

        // Start also feeds a sixth neuron with no rules, which keeps the spike after done.
        public static Part RegisterLeavingASpike(int largest = 8) => RegisterPart(largest, leaveSpike: true);

        private static Part DelayPart(int k, int startProduces)
        {
            Contract contract = DelayContract(k);
            var network = new Network(new[]
            {
                new Neuron(new[] { Rule.Standard("a", 1, startProduces) }, 0, new[] { 2 }, false, isInput: true),
                new Neuron(new[] { Rule.Standard("a+", 1, 1, k - 1) }, 0, new int[0], false),
            });
            return new Part(contract, network, PortBinding.AfterInputs(contract));
        }

        private static Part RegisterPart(int largest, bool leaveSpike)
        {
            Contract contract = RegisterContract(largest);
            const int Out = 3, Done = 4, Store = 5, Sink = 6;
            var neurons = new List<Neuron>
            {
                new Neuron(new[] { Rule.Standard("a", 1) }, 0, leaveSpike ? new[] { Store, Sink } : new[] { Store }, false, isInput: true),
                new Neuron(new[] { Rule.Standard("a", 1, 2) }, 0, new[] { Store }, false, isInput: true),
                new Neuron(new[] { Rule.Standard("a", 1), Rule.Forget("aa", 2) }, 0, new int[0], false),
                new Neuron(new[] { Rule.Forget("a", 1), Rule.Standard("aa", 2) }, 0, new int[0], false),
                new Neuron(new[] { Rule.Standard("a(aa)+", 2), Rule.Standard("a", 1, 2) }, 0, new[] { Out, Done }, false),
            };
            if (leaveSpike)
            {
                neurons.Add(new Neuron(new Rule[0], 0, new int[0], false));
            }
            return new Part(contract, new Network(neurons), PortBinding.AfterInputs(contract));
        }
    }
}
