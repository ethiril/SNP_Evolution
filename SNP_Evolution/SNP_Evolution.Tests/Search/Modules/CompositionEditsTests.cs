using SnpEvolution.Model;
using SnpEvolution.Search;
using SnpEvolution.Search.Modules;
using SnpEvolution.Specs.Parts;
using static SnpEvolution.Tests.Fixtures.CompositionFixtures;

namespace SnpEvolution.Tests.Search.Modules
{
    public class CompositionEditsTests
    {
        [Fact]
        public void RemovingAPartDropsItsWiresAndKeepsTheRest()
        {
            ModuleLibrary library = Library();
            for (int seed = 0; seed < 20; seed++)
            {
                var random = new Random(seed);
                Network network = RandomComposition.Of(library, GlueFactory(0, random), 3, random).Flatten(library);
                Composition before = Composition.Recover(network, library)!;

                Composition after = Composition.Recover(new RemovePart(library).Mutate(network, random), library)!;

                Assert.True(after.Parts.Count >= before.Parts.Count - 1);
                Assert.All(after.Parts, part => Assert.Contains(part, before.Parts));
                Assert.All(after.Wires, wire => Assert.Contains(wire, before.Wires));
                Assert.Equal(before.Glue.Count, after.Glue.Count);
            }
        }
    }
}
