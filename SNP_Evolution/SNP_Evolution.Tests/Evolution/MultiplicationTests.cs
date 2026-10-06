using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Search;
using SnpEvolution.Simulation;
using Xunit.Abstractions;

namespace SnpEvolution.Tests.Evolution
{
    // The epic's done-when: n1 x n2 solved by composition search that reuses the add loop promoted from hand-built parts.
    public class MultiplicationTests
    {
        private readonly ITestOutputHelper output;

        public MultiplicationTests(ITestOutputHelper output) => this.output = output;

        private static BenchmarkSettings Settings(ModuleLibrary library) => BenchmarkSettings.Default with
        {
            Seeds = 1,
            PopulationSize = 40,
            Repetitions = 2,
            Lexicase = true,
            CreateEngine = () => new SequentialCpuEngine(),
            Parts = library.Parts.Select(module => module.Part!).ToList(),
        };

        private static BenchmarkTask Multiply => TaskSuite.Contracts.Single(task => task.Name == "Contract multiply");

        private static AlgorithmChoice MapElites => AlgorithmCatalog.All.Single(choice => AlgorithmCatalog.IsComposition(choice.Name) && choice.Name.Contains("MAP-Elites"));

        [Fact]
        [Slow]
        public void CompositionSearchSolvesMultiplicationByReusingThePromotedAddLoop()
        {
            RunOutcome outcome = Benchmark.RunOnce(MapElites, Multiply, seed: 3, budget: 3_000, Settings(HandBuiltMachines.Library()));

            output.WriteLine($"Solved in {outcome.Evaluations} evaluations; parts in best: {string.Join(", ", outcome.Reuse!.Select(count => $"{count.Contract} {count.Direct} ({count.Nested})"))}");
            Assert.True(outcome.Solved);
            Assert.True(outcome.Promoted);
            Assert.Equal(1, outcome.Reuse!.Single(count => count.Contract == "add loop").Direct);
            Assert.Equal(2, outcome.Reuse!.Single(count => count.Contract == "register").Nested);
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
