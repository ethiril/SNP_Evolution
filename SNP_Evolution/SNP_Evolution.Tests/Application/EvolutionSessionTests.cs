using SnpEvolution.Application;
using SnpEvolution.Model;
using SnpEvolution.Search;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Benchmarking;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Search.Genome;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;
using SnpEvolution.Storage;
using static SnpEvolution.Tests.Fixtures.ModuleFixtures;

namespace SnpEvolution.Tests.Application
{
    public sealed class EvolutionSessionTests : IDisposable
    {
        private readonly TempFolder temp = new TempFolder("snp-compose");
        private readonly List<string> log = new List<string>();

        private string folder => temp.Path;

        public void Dispose() => temp.Dispose();

        private Settings Composing(CatalogEntry<Settings, BenchmarkTask> task) => new Settings
        {
            Task = task,
            RuleForm = RuleForm.Standard,
            OutputTiming = OutputTiming.Interval,
            Engine = CatalogEntries.SingleThreadEngine,
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

        private Settings OneIncrementTowardsAGap()
        {
            var library = new ModuleLibrary();
            library.AddPart(PartFixtures.Verified(ReferenceParts.Increment()), "a test");
            PartLibraryFiles.Save(library, Path.Combine(folder, "parts"));
            Settings settings = Composing(Catalog.TargetTask);
            settings.Target = new OutputTarget(TargetKind.Sequence, new[] { 1, 6, 1 });
            settings.IterativeEvolution = false;
            settings.PopulationSize = 12;
            settings.StagnationPatience = 4;
            return settings;
        }

        [Fact]
        [Slow]
        public void ASolvedContractIsPromotedSavedAndItsReuseReported()
        {
            Settings settings = Composing(SuiteTask("Contract multiply"));
            settings.HandBuiltParts = true;

            IGeneticAlgorithm run = Run(settings, seed: 7);
            RunOutput.Save(run, Path.Combine(folder, "run"), "ComposedNet", log.Add);

            Assert.True(RunLayers.IsSolved(run, settings.SelectedTask.Task));
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
            Settings settings = OneIncrementTowardsAGap();
            settings.MaxGenerations = 40;
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
        public void ProposalsCanBeTurnedOff()
        {
            Settings settings = OneIncrementTowardsAGap();
            settings.MaxGenerations = 20;
            settings.ProposeParts = false;

            IGeneticAlgorithm run = Run(settings, seed: 3);

            Assert.Empty(RunLayers.Proposals(run)!.Proposals);
        }

        [Fact]
        public void OnlyOrderedTargetsEvolveIteratively()
        {
            var sequence = new Settings { Target = new OutputTarget(TargetKind.Sequence, new[] { 1, 1, 2, 3, 5, 8 }), Task = Catalog.TargetTask };
            var set = new Settings { Target = OutputTarget.Set(new[] { 2, 4, 6, 8 }), Task = Catalog.TargetTask };

            Assert.True(EvolutionSession.IsIterative(sequence, sequence.SelectedTask, out _));
            Assert.False(EvolutionSession.IsIterative(set, set.SelectedTask, out _));
            sequence.IterativeEvolution = false;
            Assert.False(EvolutionSession.IsIterative(sequence, sequence.SelectedTask, out _));
        }
    }
}
