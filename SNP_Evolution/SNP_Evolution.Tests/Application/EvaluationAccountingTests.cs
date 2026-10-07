using SnpEvolution.Application;
using SnpEvolution.Search;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Modules;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Application
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

            Assert.InRange(evaluations.Networks, 300, 300 + Population * (1 + new ModulePolicy().SideGenerations + new ModulePolicy().IncubationGenerations));
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
    }
}
