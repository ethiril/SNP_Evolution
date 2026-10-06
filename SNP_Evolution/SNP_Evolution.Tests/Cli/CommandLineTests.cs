using SnpEvolution.Cli;
using SnpEvolution.Evolution.Modules;

namespace SnpEvolution.Tests.Cli
{
    public class CommandLineTests
    {
        [Fact]
        public void BudgetAndCompositionOptionsReachTheSettings()
        {
            var settings = new Settings();

            CommandLine.ApplyOptions(settings, new Dictionary<string, string>
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

            CommandLine.ApplyOptions(settings, new Dictionary<string, string> { ["glue-weight"] = "-1" });

            Assert.Equal(new CompositionMix().GlueEdits, settings.Composition.GlueEdits);
        }
    }
}
