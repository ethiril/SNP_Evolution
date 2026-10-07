using SnpEvolution.Application;
using SnpEvolution.Search;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Benchmarking;
using SnpEvolution.Search.Modules;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Application
{
    public class EvaluationAccountingTests
    {
        private const int Population = 10;

        // An evaluator reuses the same case objects for every network, so trials sharing the first trial's input are one per network.
        private sealed class CountingEngine : ISimulationEngine
        {
            private readonly ISimulationEngine inner;
            private long networks;

            public CountingEngine(ISimulationEngine inner) => this.inner = inner;

            public long Networks => Interlocked.Read(ref networks);

            public EngineSupport Support => inner.Support;

            public IReadOnlyList<TrialResult> Run(IReadOnlyList<Trial> trials, SimulationOptions options, Random random)
            {
                Interlocked.Add(ref networks, trials.Count(trial => ReferenceEquals(trial.Input, trials[0].Input)));
                return inner.Run(trials, options, random);
            }
        }

        // A target no small network makes in a few generations, so the run stalls and the modular loop reacts.
        private static Settings Stalling(int generations) => new Settings
        {
            Target = new OutputTarget(TargetKind.Sequence, Sequences.Fibonacci.Take(9).ToArray()),
            Task = Catalog.TargetTask,
            Engine = CatalogEntries.SingleThreadEngine,
            Repetitions = 2,
            PopulationSize = Population,
            MaxGenerations = generations,
            IterativeEvolution = false,
            StagnationRecovery = false,
            StagnationPatience = 10,
            Modules = true,
        };

        // MAP-Elites scores one batch the size of the population every generation, side runs and incubation included.
        [Fact]
        [Slow]
        public void SideRunsAndIncubationAreCountedApartFromTheMainRun()
        {
            Settings settings = Stalling(80);
            var evaluations = new EvaluationBudget();

            IGeneticAlgorithm run = EvolutionSession.Evolve(settings, settings.SelectedTask, factory => factory.NewNetwork(), new Random(3), _ => { }, evaluations);

            ModularEvolution modular = RunLayers.Modular(run)!;
            Assert.True(modular.SideRuns > 0);
            Assert.True(evaluations[EvaluationSource.SideRun] > 0);
            Assert.True(evaluations[EvaluationSource.Incubation] > 0);
            Assert.Equal((long)modular.SideGenerationsRun * Population, evaluations[EvaluationSource.SideRun] + evaluations[EvaluationSource.Incubation]);
            Assert.Equal((long)(run.Generation - 1) * Population, evaluations[EvaluationSource.Main]);
            Assert.Equal(Enum.GetValues<EvaluationSource>().Sum(source => evaluations[source]), evaluations.Networks);
        }

        [Fact]
        public void ARunStopsOnceItsEvaluationBudgetIsSpent()
        {
            Settings settings = Stalling(1000);
            settings.MaxEvaluations = 300;
            EvaluationBudget evaluations = settings.RunBudget();

            EvolutionSession.Evolve(settings, settings.SelectedTask, factory => factory.NewNetwork(), new Random(3), _ => { }, evaluations);

            var modules = new ModulePolicy();
            Assert.InRange(evaluations.Networks, 300, 300 + Population * (1 + modules.SideGenerations + modules.IncubationGenerations));
        }

        // Composition search reads its parts from the library folder, and their recorded cost is reported as paid up front.
        [Fact]
        public void ACompositionRunCountsTheLibrarysPartsAsAnUpFrontCost()
        {
            using var temp = new TempFolder("snp-composition");
            string folder = temp.Path;
            var library = new ModuleLibrary();
            library.AddPart(PartFixtures.Measured(ReferenceParts.Delay(2), new PartOrigin(1, "a test", 1234)), "a test");
            PartLibraryFiles.Save(library, folder);
            Settings settings = Stalling(5);
            settings.Modules = false;
            settings.PartLibraryFolder = folder;
            settings.Algorithm = SearchCatalog.CompositionMapElites;
            var evaluations = new EvaluationBudget();

            IGeneticAlgorithm run = EvolutionSession.Evolve(settings, settings.SelectedTask, factory => factory.NewNetwork(), new Random(3), _ => { }, evaluations);

            Assert.Equal(1234, evaluations.UpFront);
            Assert.Equal(5L * Population, evaluations.Networks);
            Assert.Contains(run.Population, individual => individual.Genes.Neurons.Any(neuron => neuron.Module != null));
            Assert.Contains("1,234", evaluations.Report().Describe());
        }

        [Fact]
        [Slow]
        public void EveryNetworkAModularRunScoresIsCharged()
        {
            var engine = new CountingEngine(new SequentialCpuEngine());
            var settings = new Settings
            {
                Target = new OutputTarget(TargetKind.Sequence, Sequences.Fibonacci.Take(9).ToArray()),
                Task = Catalog.TargetTask,
                Engine = new CatalogEntry<Settings, ISimulationEngine>("counting", _ => engine),
                Repetitions = 2,
                PopulationSize = 10,
                MaxGenerations = 60,
                IterativeEvolution = false,
                StagnationPatience = 10,
                Modules = true,
            };
            var budget = new EvaluationBudget();

            EvolutionSession.Evolve(settings, settings.SelectedTask, factory => factory.NewNetwork(), new Random(3), _ => { }, budget);

            Assert.True(budget[EvaluationSource.SideRun] > 0);
            Assert.Equal(engine.Networks, budget.Networks);
        }

        [Fact]
        [Slow]
        public void EveryNetworkABenchmarkRunScoresIsChargedRetestsIncluded()
        {
            var engine = new CountingEngine(new ExhaustiveCpuEngine());
            BenchmarkSettings settings = BenchmarkSettings.Default with { Seeds = 1, CreateEngine = () => engine };

            RunOutcome outcome = Benchmark.RunOnce(Catalog.StructuralDefault, TaskSuite.Functions.First(task => task.Name == "Compute n"), seed: 1, budget: 4000, settings);

            Assert.True(outcome.Solved);
            Assert.Equal(engine.Networks, outcome.Evaluations);
        }

        [Fact]
        [Slow]
        public void EveryNetworkAPartSearchScoresIsChargedAndItsChecksBesideThem()
        {
            var engine = new CountingEngine(new ExhaustiveCpuEngine());
            var settings = new PartSearchSettings(5_000, 1_000, 30, Catalog.StructuralDefault, () => engine);

            PartOutcome outcome = PartSearch.Evolve(PartFixtures.DelayContract(2), 1, settings, new EvaluationBudget(), _ => { });

            Assert.True(outcome.Solved);
            Assert.Equal(engine.Networks, outcome.Spent.Networks);
            Assert.True(outcome.Spent[EvaluationKind.ExhaustiveCheck] > 0);
            Assert.Equal(outcome.Spent.Networks + outcome.Spent[EvaluationKind.ExhaustiveCheck], outcome.Evaluations);
        }
    }
}
