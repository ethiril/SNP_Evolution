using SnpEvolution.Search;

namespace SnpEvolution.Tests.Cli
{
    [Collection(ProcessStateCollection.Name)]
    public class BenchmarkCommandTests
    {
        [Fact]
        public void ABenchmarkOfCompositionSearchBuildsFromThePartLibrary()
        {
            using var run = new CommandRun();
            run.CopyFolder("parts");

            string output = run.Run("benchmark", "--algorithm", SearchCatalog.CompositionMapElites.Name, "--task", "Compute n", "--seeds", "1", "--budget", "20",
                "--population", "5", "--library", "parts");

            Assert.Contains("Composition search builds from", output);
        }
    }
}
