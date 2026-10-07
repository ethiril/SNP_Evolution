using SnpEvolution.Search;
using SnpEvolution.Search.Benchmarking;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Specs.Parts;
using static SnpEvolution.Tests.Fixtures.BenchmarkFixtures;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Search.Benchmarking
{
    public class BenchmarkTests
    {
        [Fact]
        public void BenchmarkSummarisesRunsPerAlgorithmAndTask()
        {
            var best = new Individual(Identity());
            RunOutcome[] outcomes =
            {
                new RunOutcome("A", "T", 1, true, 100, 1, best),
                new RunOutcome("A", "T", 2, true, 300, 1, best),
                new RunOutcome("A", "T", 3, false, 1000, 0.5f, null),
            };

            BenchmarkRow row = Assert.Single(Benchmark.Summarise(outcomes));

            Assert.Equal((3, 2, 200.0), (row.Runs, row.Solved, row.MedianEvaluationsToSolve!.Value));
            Assert.Equal(2.5 / 3, row.MeanBestFitness, precision: 5);
            Assert.Equal(Identity().Size, row.MeanSolvedSize);
            Assert.Contains("2/3", Benchmark.FormatTable(new[] { row }));
            Assert.StartsWith("task,algorithm", Benchmark.FormatCsv(new[] { row }));
        }

        [Fact]
        [Slow]
        public void SelectorHalvesTheCandidatesUntilOneIsLeft()
        {
            List<EvolutionSearch> candidates = SearchCatalog.Evolution.Take(3).ToList();

            SelectionResult<EvolutionSearch> result = AlgorithmSelector.Select(candidates, TaskSuite.Generators[0], OneSeed, initialBudget: 60);

            Assert.Contains(result.Winner, candidates);
            Assert.Equal(new[] { 3, 2 }, result.Rounds.Select(round => round.Standings.Count));
            Assert.Equal(new[] { 60L, 120L }, result.Rounds.Select(round => round.Budget));
            Assert.Equal(result.Rounds[0].Standings.Take(2).Select(row => row.Algorithm), result.Rounds[0].Advancing);
            Assert.NotNull(result.BestFound);
        }

        [Fact]
        public void TheBenchmarkTableReportsWhichPartsTheBestNetworksHold()
        {
            var outcomes = new[]
            {
                new RunOutcome("composition", "multiply", 1, false, 100, 0.9f, null, new[] { new PartCount("add loop", 1, 0), new PartCount("register", 0, 2) }),
                new RunOutcome("composition", "multiply", 2, false, 300, 0.8f, null, new[] { new PartCount("add", 2, 0) }),
            };

            BenchmarkRow row = Benchmark.Summarise(outcomes).Single();
            string table = Benchmark.FormatTable(new[] { row });

            Assert.Equal("add 1/2, add loop 1/2", row.ReuseText);
            Assert.Equal(2, row.Reuse!.Single(use => use.Contract == "add").MeanCopies);
            Assert.Contains("Parts in best (runs)", table);
            Assert.Contains("add loop 1/2", Benchmark.FormatCsv(new[] { row }));
        }

        [Fact]
        public void AFlatRunReportsNoPartColumn()
        {
            BenchmarkRow row = Benchmark.Summarise(new[] { new RunOutcome("flat", "n + 1", 1, false, 100, 0.5f, null) }).Single();

            Assert.Null(row.Reuse);
            Assert.DoesNotContain("Parts in best", Benchmark.FormatTable(new[] { row }));
        }
    }
}
