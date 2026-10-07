using SnpEvolution.Application;
using SnpEvolution.Search;

namespace SnpEvolution.Tests.Application
{
    public class CatalogTests
    {
        [Fact]
        public void AnExactNameWinsOverNamesThatOnlyContainIt()
        {
            string[] names = { "Contract multiply 4-bit", "Contract multiply", "Contract divide" };

            Assert.Equal(new[] { "Contract multiply" }, Catalog.Matching(names, name => name, "contract MULTIPLY"));
            Assert.Equal(new[] { "Contract multiply 4-bit", "Contract multiply" }, Catalog.Matching(names, name => name, "multiply"));
        }

        [Fact]
        public void TheMenuOffersEveryGeneticAlgorithmInTheCatalog()
        {
            Assert.Equal(SearchCatalog.Evolution, Catalog.Algorithms);
        }
    }
}
