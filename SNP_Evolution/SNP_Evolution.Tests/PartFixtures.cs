using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Networks;

namespace SnpEvolution.Tests
{
    // Hand-built parts broken or padded in exactly one way, and contracts with fewer cases than the catalogue's.
    internal static class PartFixtures
    {
        public static Contract DelayContract(int k) => CatalogueEntry.OneCase(Specifications.Delay(k)).Contract;

        // The register's cases up to largest, with a latency of largest + 2, so a test runs quickly.
        public static Contract RegisterContract(int largest = 8) =>
            Specifications.Register.ContractFor(FirstParts.Each(Enumerable.Range(0, largest + 1))) with { MaxLatency = largest + 2 };

        public static Part Register(int largest = 8) => ReferenceParts.Register() with { Contract = RegisterContract(largest) };

        // Start sends done two spikes instead of one, so done fires on time and then again.
        public static Part DelayFiringDoneTwice(int k)
        {
            Contract contract = DelayContract(k);
            var network = new Network(new[]
            {
                new Neuron(new[] { Rule.Standard("a", 1, 2) }, 0, new[] { 2 }, false, isInput: true),
                new Neuron(new[] { Rule.Standard("a+", 1, 1, k - 1) }, 0, new int[0], false),
            });
            return new Part(contract, network, PortLayout.AfterInputs(contract));
        }

        // Start also feeds a sixth neuron with no rules, which keeps the spike after done.
        public static Part RegisterLeavingASpike(int largest = 8)
        {
            Contract contract = RegisterContract(largest);
            const int Store = 5, Sink = 6;
            List<Neuron> neurons = ReferenceParts.DrainingStore(countInputs: 1, startTargets: new[] { Store, Sink });
            neurons.Add(new Neuron(new Rule[0], 0, new int[0], false));
            return new Part(contract, new Network(neurons), PortLayout.AfterInputs(contract));
        }

        // An unfed sixth neuron makes it cost more for the same behaviour; it has a rule because the rule edits assume every neuron does.
        public static Part PaddedIncrement()
        {
            Part increment = ReferenceParts.Increment();
            return increment with { Network = new Network(increment.Network.Neurons.Append(new Neuron(new[] { Rule.Standard("a", 1) }, 0, new int[0], false)).ToList()) };
        }
    }
}
