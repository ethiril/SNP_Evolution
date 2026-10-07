using SnpEvolution.Application;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Search;

namespace SnpEvolution.Tests.Application
{
    public class BenchmarkServiceTests
    {
        [Fact]
        public void OnlyARacingCompositionSearchReadsThePartLibrary()
        {
            string folder = Path.Combine(Path.GetTempPath(), "snp-benchmark-" + Guid.NewGuid().ToString("N"));
            var settings = new Settings { PartLibraryFolder = folder };
            ISearch<Individual>[] composing = { Catalog.StructuralDefault, SearchCatalog.CompositionTournament };

            Assert.Null(BenchmarkService.Plan(settings, settings.BenchmarkSettings, new[] { Catalog.StructuralDefault }).Value!.PartsNote);
            Assert.StartsWith("Composition search builds from 0 part(s)", BenchmarkService.Plan(settings, settings.BenchmarkSettings, composing).Value!.PartsNote);

            Directory.CreateDirectory(folder);
            try
            {
                File.WriteAllText(Path.Combine(folder, "delay-2.json"), "{ not json");

                Assert.Contains("delay-2.json", BenchmarkService.Plan(settings, settings.BenchmarkSettings, composing).Error);
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
