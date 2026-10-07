using SnpEvolution.Application;

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
    }
}
