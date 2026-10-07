using SnpEvolution.Application;
using SnpEvolution.Search;
using SnpEvolution.Search.Fitness;

namespace SnpEvolution.Tests.Application
{
    public class BenchmarkServiceTests
    {
        [Fact]
        public void OnlyARacingCompositionSearchReadsThePartLibrary()
        {
            using var temp = new TempFolder("snp-benchmark");
            string folder = temp.Path;
            var settings = new Settings { PartLibraryFolder = folder };
            ISearch<Individual>[] composing = { Catalog.StructuralDefault, SearchCatalog.CompositionTournament };

            Assert.Null(BenchmarkService.Plan(settings, settings.BenchmarkSettings, new[] { Catalog.StructuralDefault }).Value!.PartsNote);
            Assert.StartsWith("Composition search builds from 0 part(s)", BenchmarkService.Plan(settings, settings.BenchmarkSettings, composing).Value!.PartsNote);

            temp.Made();
            File.WriteAllText(Path.Combine(folder, "delay-2.json"), "{ not json");

            Assert.Contains("delay-2.json", BenchmarkService.Plan(settings, settings.BenchmarkSettings, composing).Error);
        }
    }
}
