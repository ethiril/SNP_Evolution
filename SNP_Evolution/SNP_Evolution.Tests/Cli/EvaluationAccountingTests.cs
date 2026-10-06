using SnpEvolution.Cli;
using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Search;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Evolution.Verification;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;
using static SnpEvolution.Tests.Evolution.ModuleFixtures;

namespace SnpEvolution.Tests.Cli
{
    public class EvaluationAccountingTests
    {
        private const int Population = 10;

        // A target no small network makes in a few generations, so the run stalls and the modular loop reacts.
        private static Settings Stalling(int generations) => new Settings
        {
            Target = new OutputTarget(TargetKind.Sequence, new[] { 1, 1, 2, 3, 5, 8, 13, 21, 34 }),
            Task = Catalog.TargetTask,
            Engine = Catalog.Engines.Single(engine => engine.Name == "CPU, single thread"),
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
            var evaluations = new EvaluationCounter();

            IGeneticAlgorithm run = EvolutionSession.Evolve(settings, settings.SelectedTask, factory => factory.NewNetwork(), new Random(3), _ => { }, evaluations);

            ModularEvolution modular = EvolutionSession.Modular(run)!;
            Assert.True(modular.SideRuns > 0);
            Assert.True(evaluations[EvaluationSource.SideRun] > 0);
            Assert.True(evaluations[EvaluationSource.Incubation] > 0);
            Assert.Equal((long)modular.SideGenerationsRun * Population, evaluations[EvaluationSource.SideRun] + evaluations[EvaluationSource.Incubation]);
            Assert.Equal((long)(run.Generation - 1) * Population, evaluations[EvaluationSource.Main]);
            Assert.Equal(Enum.GetValues<EvaluationSource>().Sum(source => evaluations[source]), evaluations.Total);
        }

        [Fact]
        public void ARunStopsOnceItsEvaluationBudgetIsSpent()
        {
            Settings settings = Stalling(1000);
            settings.MaxEvaluations = 300;
            var evaluations = new EvaluationCounter();

            EvolutionSession.Evolve(settings, settings.SelectedTask, factory => factory.NewNetwork(), new Random(3), _ => { }, evaluations);

            Assert.InRange(evaluations.Total, 300, 300 + Population * (1 + new ModulePolicy().SideGenerations + new ModulePolicy().IncubationGenerations));
        }

        [Fact]
        public void RetestsOfASolvedNetworkCountAsVerification()
        {
            var counter = new EvaluationCounter();
            var evaluator = new FitnessEvaluator(new SequentialCpuEngine(), FunctionTask.Of("n", n => n, new[] { 1, 2 }),
                new SimulationOptions(40, 5, OutputTiming.Interval), 3, new Random(1), counter, EvaluationSource.SideRun);

            evaluator.Evaluate(TestNetworks.Identity());
            Assert.True(evaluator.IsReliablySolved(TestNetworks.Identity()));

            Assert.Equal(1, counter[EvaluationSource.SideRun]);
            Assert.Equal(3, counter[EvaluationSource.Verification]);
        }

        // Composition search reads its parts from the library folder, and their recorded cost is reported as paid up front.
        [Fact]
        public void ACompositionRunCountsTheLibrarysPartsAsAnUpFrontCost()
        {
            string folder = Path.Combine(Path.GetTempPath(), "snp-composition-" + Guid.NewGuid());
            try
            {
                var library = new ModuleLibrary();
                library.AddPart(Verifier.Measure(ReferenceParts.Delay(2)).ToLibraryPart(ReferenceParts.Delay(2), new PartOrigin(1, "a test", 1234)), "a test");
                PartLibraryFiles.Save(library, folder);
                Settings settings = Stalling(5);
                settings.Modules = false;
                settings.PartLibraryFolder = folder;
                settings.Algorithm = Catalog.Algorithms.First(entry => AlgorithmCatalog.IsComposition(entry.Name));
                var evaluations = new EvaluationCounter();

                IGeneticAlgorithm run = EvolutionSession.Evolve(settings, settings.SelectedTask, factory => factory.NewNetwork(), new Random(3), _ => { }, evaluations);

                Assert.Equal(1234, evaluations.UpFront);
                Assert.Equal(5L * Population, evaluations.Total);
                Assert.Contains(run.Population, individual => individual.Genes.Neurons.Any(neuron => neuron.Module != null));
                Assert.Contains("1,234", evaluations.Describe());
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
