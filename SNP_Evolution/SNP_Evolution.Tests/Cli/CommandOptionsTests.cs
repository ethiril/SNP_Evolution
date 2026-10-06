using SnpEvolution.Cli;
using SnpEvolution.Evolution.Modules;

namespace SnpEvolution.Tests.Cli
{
    public class CommandOptionsTests
    {
        [Fact]
        public void BudgetAndCompositionOptionsReachTheSettings()
        {
            var settings = new Settings();

            CommandOptions.ApplyOptions(settings, new Dictionary<string, string>
            {
                ["evaluations"] = "5000", ["library"] = "elsewhere", ["max-parts"] = "3", ["glue"] = "12", ["glue-weight"] = "0.5",
            });

            Assert.Equal(5000, settings.MaxEvaluations);
            Assert.Equal("elsewhere", settings.PartLibraryFolder);
            Assert.Equal(new CompositionMix(GlueEdits: 0.5, MaxParts: 3, MaxGlue: 12), settings.Composition);
        }

        [Fact]
        public void ANegativeGlueWeightLeavesTheDefault()
        {
            var settings = new Settings();

            CommandOptions.ApplyOptions(settings, new Dictionary<string, string> { ["glue-weight"] = "-1" });

            Assert.Equal(new CompositionMix().GlueEdits, settings.Composition.GlueEdits);
        }

        [Fact]
        public void PartLibraryOptionsReachTheSettings()
        {
            var settings = new Settings();

            CommandOptions.ApplyOptions(settings, new Dictionary<string, string> { ["hand-built"] = "leaves", ["propose"] = "off", ["proposal-budget"] = "700" });

            Assert.True(settings.HandBuiltParts);
            Assert.False(settings.HandBuiltAddLoop);
            Assert.False(settings.ProposeParts);
            Assert.Equal(700, settings.ProposalBudget);
        }

        [Fact]
        public void HandBuiltLeavesLeaveOutThePromotedAddLoop()
        {
            string missing = Path.Combine(Path.GetTempPath(), "snp-no-parts-" + Guid.NewGuid().ToString("N"));

            ModuleLibrary leaves = CommandOptions.PartLibrary(new Dictionary<string, string> { ["hand-built"] = "leaves" }, missing);
            ModuleLibrary withLoop = CommandOptions.PartLibrary(new Dictionary<string, string> { ["hand-built"] = "on" }, missing);

            Assert.NotNull(leaves.PartFor("gate"));
            Assert.Null(leaves.PartFor("add loop"));
            Assert.NotNull(withLoop.PartFor("add loop"));
        }

        [Fact]
        public void AnExactNameWinsOverNamesThatOnlyContainIt()
        {
            string[] names = { "Contract multiply 4-bit", "Contract multiply", "Contract divide" };

            Assert.Equal(new[] { "Contract multiply" }, CommandOptions.Matching(names, name => name, "contract MULTIPLY"));
            Assert.Equal(new[] { "Contract multiply 4-bit", "Contract multiply" }, CommandOptions.Matching(names, name => name, "multiply"));
        }
    }
}
