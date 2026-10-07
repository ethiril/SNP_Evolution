using SnpEvolution.Model;
using SnpEvolution.Specs.Parts;

namespace SnpEvolution.Tests.Specs.Parts
{
    public class PartLibraryTests
    {
        private static readonly PartOrigin ByHand = new PartOrigin(0, "by hand", 0);

        // The reference delay with a neuron that holds nothing and does nothing: the same behaviour at a higher cost.
        private static Part PaddedDelay(int k)
        {
            Part delay = ReferenceParts.Delay(k);
            return delay with { Network = new Network(delay.Network.Neurons.Append(new Neuron(new Rule[0], 0, new int[0], false)).ToList()) };
        }

        [Fact]
        public void PartsThatReadTheSameOnEveryCaseHaveTheSameBehaviour()
        {
            Assert.Equal(PartFixtures.Measured(ReferenceParts.Delay(2), ByHand).Behaviour, PartFixtures.Measured(PaddedDelay(2), ByHand).Behaviour);
            Assert.NotEqual(PartFixtures.Measured(ReferenceParts.Delay(2), ByHand).Behaviour, PartFixtures.Measured(PartFixtures.DelayFiringDoneTwice(2), ByHand).Behaviour);
        }

        [Fact]
        public void AddingALargerPartWithTheSameBehaviourChangesNothing()
        {
            var library = new ModuleLibrary();
            LibraryPart small = PartFixtures.Measured(ReferenceParts.Delay(2), ByHand);
            Module kept = library.AddPart(small, "first");

            Module again = library.AddPart(PartFixtures.Measured(PaddedDelay(2), ByHand), "second");

            Assert.Same(kept, again);
            Assert.Same(small, again.Part);
            Assert.Equal("first", again.Origin);
            Assert.Single(library.Parts);
        }

        [Fact]
        public void AddingASmallerPartReplacesItUnderTheSameId()
        {
            var log = new List<string>();
            var library = new ModuleLibrary(log: log.Add);
            Module kept = library.AddPart(PartFixtures.Measured(PaddedDelay(2), ByHand), "first");
            LibraryPart small = PartFixtures.Measured(ReferenceParts.Delay(2), ByHand);

            Module replaced = library.AddPart(small, "second");

            Assert.Equal(kept.Id, replaced.Id);
            Assert.Same(small, replaced.Part);
            Assert.Equal(2, replaced.Body.Neurons.Count);
            Assert.Same(replaced, library.Find(kept.Id));
            Assert.Single(library.Parts);
            Assert.Contains(log, line => line.Contains($"Module {kept.Id}") && line.Contains("replaced"));
        }

        [Fact]
        public void PartsWithDifferentResultsOnACaseAreBothKept()
        {
            var library = new ModuleLibrary();

            Module right = library.AddPart(PartFixtures.Measured(ReferenceParts.Delay(2), ByHand), "first");
            Module firingTwice = library.AddPart(PartFixtures.Measured(PartFixtures.DelayFiringDoneTwice(2), ByHand), "second");

            Assert.NotEqual(right.Id, firingTwice.Id);
            Assert.Equal(2, library.Parts.Count);
        }

        [Fact]
        public void PartsAreNeverDroppedToMakeRoomForHarvestedModules()
        {
            var library = new ModuleLibrary(capacity: 1);
            library.AddPart(PartFixtures.Measured(ReferenceParts.Delay(2), ByHand), "a part");

            library.Add(ModuleCuts.Whole(ReferenceParts.Delay(3).Network), "harvested");
            library.Add(ModuleCuts.Whole(ReferenceParts.Delay(4).Network), "harvested");

            Assert.Single(library.Parts);
            Assert.Equal(2, library.Modules.Count);
        }
    }
}
