using SnpEvolution.Application;
using SnpEvolution.Cli;
using SnpEvolution.Simulation;

namespace SnpEvolution.Tests.Cli
{
    public class SavedRunsTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        private SavedRuns Store => new SavedRuns(Path.Combine(folder, "saved-runs.json"));

        public void Dispose()
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        private static SavedRun Run(string name, Settings settings) => new SavedRun(name, DateTime.Now, RunStart.Scratch, "TargetNet", "solved", settings);

        [Fact]
        public void IsEmptyWithoutAFile()
        {
            Assert.Empty(Store.Load());
        }

        [Fact]
        public void KeepsTheSettingsAndCatalogChoices()
        {
            var settings = new Settings
            {
                Target = new OutputTarget(TargetKind.Sequence, new[] { 1, 1, 2, 3, 5 }),
                Task = Catalog.TargetTask,
                Algorithm = Catalog.Algorithms[^1],
                FitnessFunction = Catalog.FitnessFunctions[1],
                PopulationSize = 123,
                MutationRate = 0.25f,
                Modules = true,
                OutputTiming = OutputTiming.Interval,
                ModuleFiles = new[] { "a.json" },
            };

            Store.Add(Run("Fibonacci", settings));
            SavedRun loaded = Assert.Single(Store.Load());

            Assert.Equal("Fibonacci", loaded.Name);
            Assert.Equal(RunStart.Scratch, loaded.Start);
            Assert.Equal(settings.Target.Kind, loaded.Settings.Target.Kind);
            Assert.Equal(settings.Target.Values, loaded.Settings.Target.Values);
            Assert.Same(Catalog.TargetTask, loaded.Settings.Task);
            Assert.Same(settings.Algorithm, loaded.Settings.Algorithm);
            Assert.Same(settings.FitnessFunction, loaded.Settings.FitnessFunction);
            Assert.Equal(123, loaded.Settings.PopulationSize);
            Assert.Equal(0.25f, loaded.Settings.MutationRate);
            Assert.True(loaded.Settings.Modules);
            Assert.Equal(OutputTiming.Interval, loaded.Settings.OutputTiming);
            Assert.Equal(new[] { "a.json" }, loaded.Settings.ModuleFiles);
        }

        [Fact]
        public void FallsBackToTheDefaultForAChoiceNoLongerOffered()
        {
            Store.Add(Run("Old", new Settings()));
            string path = Store.Path;
            File.WriteAllText(path, File.ReadAllText(path).Replace(Catalog.StructuralDefault.Name, "An algorithm since removed"));

            Assert.Same(Catalog.StructuralDefault, Assert.Single(Store.Load()).Settings.Algorithm);
        }

        [Fact]
        public void ReplacesARunWithTheSameNameAndListsTheNewestFirst()
        {
            Store.Add(Run("First", new Settings { PopulationSize = 1 }));
            Store.Add(Run("Second", new Settings()));
            Store.Add(Run("first", new Settings { PopulationSize = 2 }));

            IReadOnlyList<SavedRun> runs = Store.Load();

            Assert.Equal(new[] { "first", "Second" }, runs.Select(run => run.Name));
            Assert.Equal(2, runs[0].Settings.PopulationSize);
        }

        [Fact]
        public void ListsEachSavedTargetOnce()
        {
            var target = new OutputTarget(TargetKind.Set, new[] { 2, 4 });
            Store.Add(Run("A", new Settings { Target = target, Task = Catalog.TargetTask }));
            Store.Add(Run("B", new Settings { Target = target, Task = Catalog.TargetTask }));
            Store.Add(Run("Suite", new Settings { Target = target, Task = Catalog.Tasks[1] }));

            Assert.Equal("B", Assert.Single(Store.Targets()).Name);
        }

        [Fact]
        public void RemovesARun()
        {
            Store.Add(Run("A", new Settings()));
            Store.Remove("A");

            Assert.Empty(Store.Load());
        }
    }
}
