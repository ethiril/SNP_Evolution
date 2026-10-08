using SnpEvolution.Search;
using SnpEvolution.Search.Benchmarking;
using SnpEvolution.Search.Modules;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Verification;
using Xunit.Abstractions;

namespace SnpEvolution.Tests.Search.Modules
{
    public class HandBuiltMachinesTests
    {
        private readonly ITestOutputHelper output;

        public HandBuiltMachinesTests(ITestOutputHelper output) => this.output = output;

        [Fact]
        public void TheAddLoopMeetsItsContractAndDrainsALargeAccumulatorBeforeDone()
        {
            var library = new ModuleLibrary();
            foreach (Part part in HandBuiltParts.All())
            {
                library.AddPart(PartFixtures.Verified(part), "a test");
            }
            (Composition loop, PortBinding binding) = HandBuiltMachines.AddLoop(library);
            Contract contract = ArithmeticParts.AddLoop();
            Specification specification = Assert.IsType<Specification>(Specification.For(contract));
            ContractCase largeA = specification.Expected(new Dictionary<string, int> { ["a"] = 14, ["b"] = 0, ["n"] = 0 });

            PartMeasurement measurement = Verifier.Measure(new Part(contract with { Cases = contract.Cases.Append(largeA).ToList() }, loop.Flatten(library), binding), new EvaluationBudget());

            output.WriteLine($"{measurement.Cost}, latency {measurement.Latency}");
            output.WriteLine(measurement.Description);
            Assert.IsType<Verdict.Passed>(measurement.Verdict);
        }

        [Fact]
        [Slow]
        public void TheHandBuiltLibraryHoldsThePromotedAddLoopBuiltFromParts()
        {
            var log = new List<string>();

            ModuleLibrary library = HandBuiltMachines.Library(log.Add);

            LibraryPart loop = library.PartFor("add loop")!.Part!;
            Assert.True(loop.IsComposite);
            Assert.False(library.PartFor("register")!.Part!.IsComposite);
            Assert.Equal(7, loop.Recipe!.Parts.Count);
            Assert.True(loop.Cost.Neurons > ModuleLibrary.MaxModuleNeurons);
            Assert.Equal(BoundedCheck.Admission(loop.Contract).MaxBound, loop.Proven?.UpTo);
            output.WriteLine(string.Join(Environment.NewLine, log));
        }

        [Fact]
        [Slow]
        public void CompositionSearchSolvesMultiplicationByReusingThePromotedAddLoop()
        {
            BenchmarkTask multiply = TaskSuite.Contracts.Single(task => task.Name == "Contract multiply");
            BenchmarkSettings settings = BenchmarkFixtures.WithParts(HandBuiltMachines.Library()) with
            {
                PopulationSize = 40,
                Repetitions = 2,
                Lexicase = true,
                CreateEngine = () => new SequentialCpuEngine(),
            };

            RunOutcome outcome = Benchmark.RunOnce(SearchCatalog.CompositionMapElites, multiply, seed: 1, budget: 3_000, settings);

            output.WriteLine($"Solved in {outcome.Evaluations} evaluations; parts in best: {string.Join(", ", outcome.Reuse!.Select(count => $"{count.Contract} {count.Direct} ({count.Nested})"))}");
            Assert.True(outcome.Solved);
            Assert.True(outcome.Promoted);
            Assert.Equal(1, outcome.Reuse!.Single(count => count.Contract == "add loop").Direct);
            Assert.Equal(2, outcome.Reuse!.Single(count => count.Contract == "register").Nested);
        }
    }
}
