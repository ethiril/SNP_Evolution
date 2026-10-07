using SnpEvolution.Model;
using SnpEvolution.Search;
using SnpEvolution.Search.Genome;
using SnpEvolution.Search.Modules;
using SnpEvolution.Search.Operators;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Verification;
using static SnpEvolution.Tests.Fixtures.ModuleFixtures;
using static SnpEvolution.Tests.Fixtures.PartWiringFixtures;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Search.Modules
{
    public class PartMutationsTests
    {
        private static NetworkFactory Factory(int maxNeurons, Random random) =>
            Factories.Networks(new GenomeSpace(InputCount: 2, RuleForm: RuleForm.Standard, MaxNeurons: maxNeurons), random);

        // Every synapse from one copy's port to another copy's port joins ports that fit.
        private static void AssertCrossCopyWiresFit(Network network, IReadOnlyList<PartCopy> copies)
        {
            Dictionary<int, CopyPort> ports = copies.SelectMany(copy => copy.Ports).ToDictionary(port => port.Position);
            foreach (CopyPort from in ports.Values)
            {
                IEnumerable<CopyPort> targets = network.Neurons[from.Position - 1].Connections.Where(ports.ContainsKey).Select(target => ports[target]).Where(target => target.Copy != from.Copy);
                Assert.All(targets, target => Assert.True(PartWiring.Fits(from.Port, target.Port), $"{from.Port.Name} > {target.Port.Name}"));
            }
        }

        [Fact]
        public void RewiringOnlyEverJoinsFittingPortsAndNeverFeedsAnInput()
        {
            var library = new ModuleLibrary();
            Module increment = library.AddPart(PartFixtures.Verified(ReferenceParts.Increment()), "a test");
            Network chained = Chained(library, increment, 1);
            Network network = ModuleEdits.Insert(chained, increment, 3, MaxNeurons, library, new Random(1));
            var rewire = new RewirePort(library);
            var seen = new HashSet<string>();

            for (int seed = 0; seed < 40; seed++)
            {
                Network rewired = rewire.Mutate(network, new Random(seed));

                IReadOnlyList<PartCopy> copies = PartWiring.Copies(rewired, library);
                Assert.Equal(3, copies.Count);
                AssertCrossCopyWiresFit(rewired, copies);
                Assert.All(rewired.Neurons, neuron => Assert.DoesNotContain(neuron.Connections, target => rewired.Neurons[target - 1].IsInput));
                seen.Add(string.Join(" ", PartWiring.Wires(rewired, library).Select(WireText)));
            }
            Assert.True(seen.Count > 1);
        }

        [Fact]
        public void AGlueNeuronSitsOnAWireAndThePairStillAddsTwo()
        {
            var library = new ModuleLibrary();
            Module increment = library.AddPart(PartFixtures.Verified(ReferenceParts.Increment()), "a test");
            Network network = Chained(library, increment, 1);
            var random = new Random(1);

            Network glued = new AddGlueNeuron(library, Factory(MaxNeurons, random)).Mutate(network, random);

            Assert.Equal(network.Neurons.Count + 1, glued.Neurons.Count);
            Assert.Single(PartWiring.Wires(glued, library));
            Assert.Null(glued.Neurons[^1].Module);
            PartMeasurement measurement = MeasurePlusTwo(glued, library);
            Assert.True(measurement.Verdict is Verdict.Passed, measurement.Description);
        }

        [Fact]
        public void NoGlueNeuronIsAddedWithoutRoomForIt()
        {
            var library = new ModuleLibrary();
            Module increment = library.AddPart(PartFixtures.Verified(ReferenceParts.Increment()), "a test");
            Network network = Chained(library, increment, 1);
            var random = new Random(1);

            Assert.Same(network, new AddGlueNeuron(library, Factory(network.Neurons.Count, random)).Mutate(network, random));
        }

        [Fact]
        public void APartIsSwappedForACheaperOneWithTheSameContractAndKeepsItsWires()
        {
            var library = new ModuleLibrary();
            Module increment = library.AddPart(PartFixtures.Verified(PartFixtures.PaddedIncrement()), "a test");
            Network padded = Chained(library, increment, 1);
            library.AddPart(PartFixtures.Verified(ReferenceParts.Increment()), "a cheaper test");
            var swap = new SwapPart(library);

            Network once = swap.Mutate(padded, new Random(1));
            Network twice = swap.Mutate(once, new Random(1));

            Assert.Equal(new[] { 12, 11, 10 }, new[] { padded, once, twice }.Select(network => network.Neurons.Count));
            Assert.Same(twice, swap.Mutate(twice, new Random(1)));
            Assert.Equal(new[] { "1.out>2.n", "1.done>2.start" }, PartWiring.Wires(twice, library).Select(WireText));
            Assert.Equal(new[] { true, true }, twice.Neurons.Take(2).Select(neuron => neuron.IsInput));
            PartMeasurement measurement = MeasurePlusTwo(twice, library);
            Assert.True(measurement.Verdict is Verdict.Passed, measurement.Description);
        }

        // A role on a neuron with no port of the same name would be lost, so that copy is not swapped.
        [Fact]
        public void ACopyWithARoleOffItsPortsIsNotSwapped()
        {
            var library = new ModuleLibrary();
            Module increment = library.AddPart(PartFixtures.Verified(PartFixtures.PaddedIncrement()), "a test");
            Network padded = Chained(library, increment, 1);
            library.AddPart(PartFixtures.Verified(ReferenceParts.Increment()), "a cheaper test");
            var swap = new SwapPart(library);

            Network outputOnPadding = padded.WithNeuron(11, padded.Neurons[11].WithRoles(true, false));
            Assert.All(Enumerable.Range(0, 10), seed => Assert.Single(swap.Mutate(outputOnPadding, new Random(seed)).Neurons, neuron => neuron.IsOutput));
        }

        [Fact]
        public void PartEditsAreOnlyOfferedWhenTheLibraryHoldsParts()
        {
            var harvested = new ModuleLibrary();
            harvested.Add(ModuleCuts.Whole(PingPong()), "a test");
            var withParts = new ModuleLibrary();
            withParts.AddPart(PartFixtures.Verified(ReferenceParts.Increment()), "a test");
            NetworkFactory factory = Factory(MaxNeurons, new Random(1));

            IEnumerable<WeightedEdit> PartEdits(ModuleLibrary library) =>
                WeightedMutation.Structural(1, factory, modules: new ModuleSupport(library)).Edits.Where(edit => edit.Edit is RewirePort or AddGlueNeuron or SwapPart);

            Assert.Empty(PartEdits(harvested));
            Assert.Equal(3, PartEdits(withParts).Count());
        }
    }
}
