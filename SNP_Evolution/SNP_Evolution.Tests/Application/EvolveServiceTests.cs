using SnpEvolution.Application;
using SnpEvolution.Search;

namespace SnpEvolution.Tests.Application
{
    public class EvolveServiceTests
    {
        [Fact]
        public void ACompositionRunOnABrokenLibraryIsATypedErrorNamingTheFile()
        {
            using var temp = new TempFolder("snp-evolve").Made();
            string folder = temp.Path;
            File.WriteAllText(Path.Combine(folder, "delay-2.json"), "{ not json");
            var settings = new Settings { Algorithm = SearchCatalog.CompositionTournament, PartLibraryFolder = folder };

            EvolveResult result = EvolveService.Run(new EvolveRequest(settings, new Random(1), "Composed", Folder: folder), _ => { });

            Assert.Null(result.Run);
            Assert.Contains("delay-2.json", result.Error);
        }
    }
}
