using SnpEvolution.Model;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Verification;

namespace SnpEvolution.Tests.Fixtures
{
    // Hand-built parts broken or padded in exactly one way, and contracts with fewer cases than the catalogue's.
    internal static class PartFixtures
    {
        public static readonly PartOrigin ATest = new PartOrigin(1, "a test", 0);

        public static readonly PartOrigin ByHand = new PartOrigin(0, "by hand", 0);

        // The hand-built parts by their place in HandBuiltParts.All(), for a theory over each.
        public static TheoryData<int> HandBuilt => new TheoryData<int>(Enumerable.Range(0, HandBuiltParts.All().Count));

        // Measured on the exhaustive engine and recorded as a library part would be.
        public static LibraryPart Measured(Part part, PartOrigin? origin = null) =>
            Verifier.Measure(part, new EvaluationBudget()).ToLibraryPart(part, origin ?? ATest);

        // Measured, and required to pass, so a test never builds on a part that fails its own contract.
        public static LibraryPart Verified(Part part)
        {
            PartMeasurement measurement = Verifier.Measure(part, new EvaluationBudget());
            Assert.True(measurement.Verdict is Verdict.Passed, measurement.Description);
            return measurement.ToLibraryPart(part, ATest);
        }

        public static Part HandBuiltRegister() => HandBuiltParts.All().Single(part => part.Contract.Name == "register");

        // A library folder whose one part file is not JSON, for the typed load error.
        public static void WriteBrokenLibrary(string folder)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "delay-2.json"), "{ not json");
        }

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

        // The reference delay with a neuron that holds nothing and does nothing: the same behaviour at a higher cost.
        public static Part PaddedDelay(int k)
        {
            Part delay = ReferenceParts.Delay(k);
            return delay with { Network = new Network(delay.Network.Neurons.Append(new Neuron(new Rule[0], 0, new int[0], false)).ToList()) };
        }

        // The hand-built register, except that a store holding 41 spikes (n = 20 and start) sends two spikes instead of one.
        public static Part RegisterFailingAtTwenty()
        {
            Part register = HandBuiltRegister();
            const int Store = 4;
            List<Neuron> neurons = register.Network.Neurons.ToList();
            neurons[Store] = new Neuron(
                new[] { Rule.Standard("a(aa){1,19}|a(aa){21,}", 2), Rule.Standard("a{41}", 2, 2), Rule.Standard("a", 1, 2) }, 0, neurons[Store].Connections, false);
            return register with { Network = new Network(neurons) };
        }
    }
}
