using SnpEvolution.Specs.Parts;
using static SnpEvolution.Tests.Fixtures.ModuleFixtures;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Specs.Parts
{
    public class ModuleLibraryTests
    {
        [Fact]
        public void TheLibraryKeepsEachPartOnceAndDropsTheLeastHelpfulWhenFull()
        {
            var library = new ModuleLibrary(capacity: 2);
            Module first = ModuleOf(library, Chain());
            Module second = ModuleOf(library, PingPong());

            Assert.Same(first, ModuleOf(library, Chain()));
            library.Credit(first.Id, improved: false);
            library.Credit(second.Id, improved: true);
            ModuleOf(library, AlwaysOutputsOne());

            Assert.Equal(2, library.Modules.Count);
            Assert.Null(library.Find(first.Id));
            Assert.Equal((1, 1), (second.Uses, second.Wins));
        }

        [Fact]
        public void PartsThatReadTheSameOnEveryCaseHaveTheSameBehaviour()
        {
            Assert.Equal(PartFixtures.Measured(ReferenceParts.Delay(2), PartFixtures.ByHand).Behaviour, PartFixtures.Measured(PartFixtures.PaddedDelay(2), PartFixtures.ByHand).Behaviour);
            Assert.NotEqual(PartFixtures.Measured(ReferenceParts.Delay(2), PartFixtures.ByHand).Behaviour, PartFixtures.Measured(PartFixtures.DelayFiringDoneTwice(2), PartFixtures.ByHand).Behaviour);
        }

        [Fact]
        public void AddingALargerPartWithTheSameBehaviourChangesNothing()
        {
            var library = new ModuleLibrary();
            LibraryPart small = PartFixtures.Measured(ReferenceParts.Delay(2), PartFixtures.ByHand);
            Module kept = library.AddPart(small, "first");

            Module again = library.AddPart(PartFixtures.Measured(PartFixtures.PaddedDelay(2), PartFixtures.ByHand), "second");

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
            Module kept = library.AddPart(PartFixtures.Measured(PartFixtures.PaddedDelay(2), PartFixtures.ByHand), "first");
            LibraryPart small = PartFixtures.Measured(ReferenceParts.Delay(2), PartFixtures.ByHand);

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

            Module right = library.AddPart(PartFixtures.Measured(ReferenceParts.Delay(2), PartFixtures.ByHand), "first");
            Module firingTwice = library.AddPart(PartFixtures.Measured(PartFixtures.DelayFiringDoneTwice(2), PartFixtures.ByHand), "second");

            Assert.NotEqual(right.Id, firingTwice.Id);
            Assert.Equal(2, library.Parts.Count);
        }

        [Fact]
        public void PartsAreNeverDroppedToMakeRoomForHarvestedModules()
        {
            var library = new ModuleLibrary(capacity: 1);
            library.AddPart(PartFixtures.Measured(ReferenceParts.Delay(2), PartFixtures.ByHand), "a part");

            library.Add(ModuleCuts.Whole(ReferenceParts.Delay(3).Network), "harvested");
            library.Add(ModuleCuts.Whole(ReferenceParts.Delay(4).Network), "harvested");

            Assert.Single(library.Parts);
            Assert.Equal(2, library.Modules.Count);
        }
    }
}
