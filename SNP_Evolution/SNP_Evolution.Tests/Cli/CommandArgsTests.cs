using SnpEvolution.Application;
using SnpEvolution.Cli;
using SnpEvolution.Search.Modules;

namespace SnpEvolution.Tests.Cli
{
    [Collection(ProcessStateCollection.Name)]
    public class CommandArgsTests
    {
        private static string Problem(params string[] line)
        {
            Command command = CommandRegistry.Find(line[0])!;
            Assert.Null(CommandArgs.Parse(command, line, out string error));
            return error;
        }

        // The settings evolve would run with, from the menu's defaults and the options given.
        private static Settings Evolving(params string[] options)
        {
            string[] line = new[] { "evolve", "--target", "1,2" }.Concat(options).ToArray();
            CommandArgs args = CommandArgs.Parse(CommandRegistry.Find("evolve")!, line, out string error) ?? throw new ArgumentException(error);
            return TargetArgs.Settings(args, SettingOptions.Evolve)!;
        }

        [Fact]
        public void AnUnknownOptionIsRefusedByName() =>
            Assert.Equal("evolve has no option --genrations.", Problem("evolve", "--target", "1,2", "--genrations", "5"));

        [Fact]
        public void AMissingValueIsRefusedRatherThanShiftingTheOptionsAfterIt() =>
            Assert.Equal("--target needs a value: --target VALUES.", Problem("evolve", "--target", "--seed", "1"));

        [Fact]
        public void AValueTheOptionCannotTakeIsRefused() =>
            Assert.Equal("--seed takes a whole number of 0 or more, not 'one'.", Problem("evolve", "--target", "1,2", "--seed", "one"));

        [Fact]
        public void AnOptionGivenTwiceIsRefused() =>
            Assert.Equal("--seed is given more than once.", Problem("evolve", "--target", "1,2", "--seed", "1", "--seed", "2"));

        [Fact]
        public void ARequiredOptionLeftOutIsNamed() =>
            Assert.Equal("evolve needs --target VALUES.", Problem("evolve", "--seed", "1"));

        [Fact]
        public void AWordThatIsNotAnOptionIsRefused() =>
            Assert.Equal("'1,2' is not an option; options start with --.", Problem("evolve", "1,2"));

        [Theory]
        [InlineData("no-such-command")]
        [InlineData("evolve", "--target")]
        [InlineData("evolve", "--target", "1,2", "--modules", "yes")]
        [InlineData("export-verilog", "--network", "no-such-network.json")]
        [InlineData("advise", "--target", "1,x")]
        [InlineData("reach", "--target", "1,2", "--evaluations", "100", "--setups", "no-such-setup")]
        [InlineData("evolve-parts", "--only", "no such contract", "--library", "no-such-library-folder")]
        [InlineData("compose", "--task", "Contract")]
        [InlineData("select", "--task", "Contract")]
        [InlineData("evolve-parts", "--profile", "hardwre")]
        [InlineData("evolve", "--target", "1,2", "--glue-weight", "-1")]
        [InlineData("compose", "--task", "no such task")]
        [InlineData("evolve", "--target", "1,2", "--algorithm", "no such search")]
        public void BadCommandLinesExitWithUsage(params string[] line)
        {
            using var console = new ConsoleCapture();

            Assert.Equal((int)ExitCode.Usage, CommandLine.Run(line));
        }

        [Fact]
        public void BudgetAndCompositionOptionsReachTheSettings()
        {
            Settings settings = Evolving("--evaluations", "5000", "--library", "elsewhere", "--max-parts", "3", "--glue", "12", "--glue-weight", "0.5");

            Assert.Equal(5000, settings.MaxEvaluations);
            Assert.Equal("elsewhere", settings.PartLibraryFolder);
            Assert.Equal(new CompositionMix(GlueEdits: 0.5, MaxParts: 3, MaxGlue: 12), settings.Composition);
        }

        [Fact]
        public void TheHardwareProfileIsOffUntilAskedForAndCanBeLiftedAgain()
        {
            Assert.False(Evolving().GenomeSpace(1).HardwareProfile);
            Assert.True(Evolving("--profile", "hardware").GenomeSpace(1).HardwareProfile);
            Assert.False(Evolving("--profile", "none").HardwareProfile);
        }

        [Fact]
        public void PartLibraryOptionsReachTheSettings()
        {
            Settings settings = Evolving("--hand-built", "leaves", "--propose", "off", "--proposal-budget", "700");

            Assert.True(settings.HandBuiltParts);
            Assert.False(settings.HandBuiltAddLoop);
            Assert.False(settings.ProposeParts);
            Assert.Equal(700, settings.ProposalBudget);
        }

        [Fact]
        public void ModuleFilesTurnModulesOn()
        {
            Settings settings = Evolving("--modules", "off", "--module-files", "a.json, b.json");

            Assert.Equal(new[] { "a.json", "b.json" }, settings.ModuleFiles);
            Assert.True(settings.Modules);
        }

        [Fact]
        public void OptionsGivenOverrideTheAdvisorsSuggestions()
        {
            string[] line = { "evolve", "--target", "1,2", "--advise", "on", "--population", "3" };
            CommandArgs args = CommandArgs.Parse(CommandRegistry.Find("evolve")!, line, out string error) ?? throw new ArgumentException(error);

            Assert.Equal(3, EvolveCommand.Configured(args, _ => { })!.PopulationSize);
        }

        [Fact]
        public void TheKindOfTargetIsReadIgnoringCase() =>
            Assert.Equal(TargetKind.Set, Evolving("--kind", "SET").Target.Kind);

        [Fact]
        public void AnAlgorithmNamedExactlyWinsOverThoseContainingItsName() =>
            Assert.Equal(Catalog.StructuralDefault, Evolving("--algorithm", Catalog.StructuralDefault.Name.ToUpperInvariant()).Algorithm);
    }
}
