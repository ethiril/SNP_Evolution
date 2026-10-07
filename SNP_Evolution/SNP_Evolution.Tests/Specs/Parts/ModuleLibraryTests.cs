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

    }
}
