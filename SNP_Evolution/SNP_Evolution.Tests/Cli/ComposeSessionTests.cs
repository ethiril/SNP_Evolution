using SnpEvolution.Cli;
using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Genome;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Search;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;
using static SnpEvolution.Tests.Evolution.ModuleFixtures;

namespace SnpEvolution.Tests.Cli
{
    public sealed class ComposeSessionTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "snp-compose-" + Guid.NewGuid().ToString("N"));
        private readonly List<string> log = new List<string>();

        public void Dispose()
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        private Settings Composing(CatalogEntry<Settings, BenchmarkTask> task) => new Settings
        {
            Task = task,
            RuleForm = RuleForm.Standard,
            OutputTiming = OutputTiming.Interval,
            Engine = Catalog.Engines.Single(engine => engine.Name == "CPU, single thread"),
            Algorithm = SearchCatalog.CompositionMapElites,
            Repetitions = 2,
            PopulationSize = 40,
            MaxGenerations = 400,
            Lexicase = true,
            PartLibraryFolder = Path.Combine(folder, "parts"),
        };

        private IGeneticAlgorithm Run(Settings settings, int seed, EvaluationBudget? evaluations = null) =>
            EvolutionSession.Evolve(settings, settings.SelectedTask, factory => factory.NewNetwork(), new Random(seed), log.Add, evaluations ?? new EvaluationBudget());

        private static CatalogEntry<Settings, BenchmarkTask> SuiteTask(string name) => Catalog.Tasks.Single(task => task.Name == name);

        // The done-when through the session the compose command runs: solved, promoted, saved, and the add loop reported as reused.
        [Fact]
        [Slow]
        public void ASolvedContractIsPromotedSavedAndItsReuseReported()
        {
            Settings settings = Composing(SuiteTask("Contract multiply"));
            settings.HandBuiltParts = true;

            IGeneticAlgorithm run = Run(settings, seed: 7);
            RunOutput.Save(run, Path.Combine(folder, "run"), "ComposedNet", log.Add);

            Assert.True(EvolutionSession.IsSolved(run, settings.SelectedTask.Task));
            Assert.Contains(log, line => line.StartsWith("Promoted the composition for multiply") && line.Contains("built from add loop"));
            LibraryPart multiply = PartLibraryFiles.Load(settings.PartLibraryFolder).PartFor("multiply")!.Part!;
            Assert.True(multiply.IsComposite);
            string parts = File.ReadAllText(Path.Combine(folder, "run", "ComposedNet-parts.txt"));
            Assert.Contains("add loop (promoted)", parts);
            Assert.Contains("The best network reuses promoted part(s): add loop x1.", parts);
        }

        // A library of one increment cannot make gaps, so the run stalls and asks for the timer it misses.
        [Fact]
        [Slow]
        public void TheRunLogListsEachProposalWithItsOutcome()
        {
            var library = new ModuleLibrary();
            library.AddPart(Verified(ReferenceParts.Increment()), "a test");
            PartLibraryFiles.Save(library, Path.Combine(folder, "parts"));
            Settings settings = Composing(Catalog.TargetTask);
            settings.Target = new OutputTarget(TargetKind.Sequence, new[] { 1, 6, 1 });
            settings.IterativeEvolution = false;
            settings.PopulationSize = 12;
            settings.MaxGenerations = 40;
            settings.StagnationPatience = 4;
            settings.ProposalBudget = 5_000;
            var evaluations = new EvaluationBudget();

            IGeneticAlgorithm run = Run(settings, seed: 3, evaluations);
            RunOutput.Save(run, Path.Combine(folder, "run"), "Net", log.Add);

            string parts = File.ReadAllText(Path.Combine(folder, "run", "Net-parts.txt"));
            Assert.Contains("part(s) proposed", parts);
            Assert.Contains("proposed parts", evaluations.Report().Describe());
            Assert.Matches(@"generation \d+: delay \d+ \(failing checks, nothing passes gap \d+ \(\d+\)\): (solved in \d+ evaluations, kept as module \d+|not solved)", parts);
        }

        [Fact]
        public void ComposeRefusesATaskNameThatMatchesNoTask() =>
            Assert.Equal(1, CommandLine.Run(new[] { "compose", "--task", "no such task" }));

        [Fact]
        public void AContractRunJustShortOfAPerfectScoreIsNotSolved()
        {
            var individual = new Individual(TestNetworks.AlwaysOutputsOne());
            individual.Record(new FitnessResult(0.99f, Array.Empty<int>(), "", Exact: true));
            var run = new FixedPopulation(individual);

            Assert.True(EvolutionSession.IsSolved(run));
            Assert.False(EvolutionSession.IsSolved(run, new ContractTask(PartFixtures.RegisterContract())));
        }

        private sealed class FixedPopulation(Individual best) : IGeneticAlgorithm
        {
            public IReadOnlyList<Individual> Population => new[] { best };

            public int Generation => 1;

            public Individual? Best => best;

            public IReadOnlyList<IReadOnlyList<float>> FitnessHistory => Array.Empty<IReadOnlyList<float>>();

            public void NextGeneration() { }

            public void Immigrate(IReadOnlyList<Network> newcomers) { }

            public void Rescore() { }
        }

        [Fact]
        public void ProposalsCanBeTurnedOff()
        {
            var library = new ModuleLibrary();
            library.AddPart(Verified(ReferenceParts.Increment()), "a test");
            PartLibraryFiles.Save(library, Path.Combine(folder, "parts"));
            Settings settings = Composing(Catalog.TargetTask);
            settings.Target = new OutputTarget(TargetKind.Sequence, new[] { 1, 6, 1 });
            settings.IterativeEvolution = false;
            settings.PopulationSize = 12;
            settings.MaxGenerations = 20;
            settings.StagnationPatience = 4;
            settings.ProposeParts = false;

            IGeneticAlgorithm run = Run(settings, seed: 3);

            Assert.Empty(EvolutionSession.Proposals(run)!.Proposals);
        }
    }
}
