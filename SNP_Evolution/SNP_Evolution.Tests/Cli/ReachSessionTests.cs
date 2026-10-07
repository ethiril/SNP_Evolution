using SnpEvolution.Cli;
using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Verification;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Cli
{
    public class ReachSessionTests
    {
        private const int Population = 10;
        private const long PartCost = 1234;

        private static Settings Budgeted(string folder, long budget) => new Settings
        {
            Target = new OutputTarget(TargetKind.Sequence, new[] { 1, 1, 2, 3, 5, 8, 13 }),
            Task = Catalog.TargetTask,
            Repetitions = 3,
            PopulationSize = Population,
            MaxGenerations = 1000,
            IterativeEvolution = false,
            StagnationRecovery = false,
            PartLibraryFolder = folder,
            MaxEvaluations = budget,
        };

        private static ReachSession.Setup Named(string name) => ReachSession.Setups.Single(setup => setup.Name == name);

        [Fact]
        public void ReachCountsChecksPassedInOrderAllowingForRounding()
        {
            Assert.Equal(2, ReachSession.Reach(new[] { 1f, 3 * (1f / 3), 0.5f, 1f }));
            Assert.Equal(2, ReachSession.Reach(new[] { 1f, 0.99999f, 0.999f }));
        }

        [Fact]
        [Slow]
        public void CompositionSearchSpendsOnlyWhatThePartsLeaveOfTheSharedBudget()
        {
            string folder = Path.Combine(Path.GetTempPath(), "snp-reach-" + Guid.NewGuid());
            try
            {
                var library = new ModuleLibrary();
                library.AddPart(Verifier.Measure(ReferenceParts.Delay(2), new EvaluationBudget()).ToLibraryPart(ReferenceParts.Delay(2), new PartOrigin(1, "a test", PartCost)), "a test");
                PartLibraryFiles.Save(library, folder);
                Settings settings = Budgeted(folder, PartCost + 100);

                ReachSession.Outcome composition = ReachSession.RunOnce(settings, Named("composition"), 1, PartCost);
                ReachSession.Outcome flat = ReachSession.RunOnce(settings, Named("flat"), 1, PartCost);

                Assert.Equal(PartCost, composition.UpFront);
                Assert.InRange(composition.Evaluations, 100, PartCost - 1);
                Assert.True(flat.Evaluations >= PartCost + 100);
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
