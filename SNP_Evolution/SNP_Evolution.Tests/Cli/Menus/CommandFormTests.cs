using SnpEvolution.Application;
using SnpEvolution.Cli;
using SnpEvolution.Search;

namespace SnpEvolution.Tests.Cli.Menus
{
    public class CommandFormTests
    {
        private static readonly Dictionary<Option, string> Nothing = new();

        private static Settings TargetOneTwoThree()
        {
            OutputTarget.TryParse(TargetKind.Sequence, "1,2,3", out OutputTarget target);
            var settings = new Settings { Target = target, MutationRate = 0.3f };
            settings.UseTask(Catalog.TargetTask);
            return settings;
        }

        private static CommandForm Form(Command command, Settings settings, Func<Settings, IEnumerable<(Option, string)>>? presets = null, Dictionary<Option, string>? typed = null) =>
            new CommandForm(new CommandItem("test", command, presets), settings, typed ?? Nothing);

        private static Settings Parsed(CommandForm form, IReadOnlyList<string> line, Settings settings) =>
            TargetArgs.Settings(CommandArgs.Parse(form.Command, line, out string error, settings) ?? throw new ArgumentException(error), SettingOptions.Evolve)!;

        [Fact]
        public void PresetsFillTheLineFromTheSettings()
        {
            CommandForm form = Form(CommandRegistry.Get<EvolveCommand>(), TargetOneTwoThree(), MainMenu.Selected);

            Assert.Equal(new[] { "evolve", "--target", "1,2,3", "--kind", "sequence" }, form.Line());
        }

        [Fact]
        public void WhatIsTypedWinsOverThePresets()
        {
            var typed = new Dictionary<Option, string> { [CommonOptions.Target] = "4,5" };

            CommandForm form = Form(CommandRegistry.Get<EvolveCommand>(), TargetOneTwoThree(), MainMenu.Selected, typed);

            Assert.Equal("4,5", form.Text(CommonOptions.Target));
        }

        [Fact]
        public void ASuiteTaskIsPresetAsTheTask()
        {
            var settings = new Settings();
            settings.UseTask(Catalog.Tasks.First(task => task.Name == "Compute n"));

            Assert.Equal(new[] { "evolve", "--task", "Compute n" }, Form(CommandRegistry.Get<EvolveCommand>(), settings, MainMenu.Selected).Line());
        }

        // The menu runs the form's line from its settings; the line it shows runs from the defaults to the same settings.
        [Fact]
        public void TheLineShownGivesTheSettingsTheMenuRunsWith()
        {
            Settings menu = TargetOneTwoThree();
            menu.Engine = Catalog.Engines.Last();
            menu.Modules = true;
            CommandForm form = Form(CommandRegistry.Get<EvolveCommand>(), menu, MainMenu.Selected);

            Settings fromMenu = Parsed(form, form.Line(), menu);
            Settings fromShell = Parsed(form, form.Equivalent(), new Settings());

            Assert.Contains("--mutation-rate", form.Equivalent());
            Assert.All(SettingOptions.Evolve, setting => Assert.Equal(setting.Text(fromMenu), setting.Text(fromShell)));
        }

        [Fact]
        public void TheLineShownLeavesOutSettingsAtTheirDefaults()
        {
            CommandForm form = Form(CommandRegistry.Get<EvolveCommand>(), new Settings(), MainMenu.Selected);

            Assert.Equal(form.Line(), form.Equivalent());
        }

        [Fact]
        public void TheSettingsShownAreTheOnesTheCommandStartsFrom() =>
            Assert.Equal(SearchCatalog.CompositionTournament, Form(CommandRegistry.Get<ComposeCommand>(), new Settings()).Preview().Algorithm);

        [Fact]
        public void ASettingTypedOnTheFormChangesOnlyThatRun()
        {
            Settings menu = new Settings();
            var typed = new Dictionary<Option, string> { [SettingOptions.Population.Option] = "7" };

            CommandForm form = Form(CommandRegistry.Get<EvolveCommand>(), menu, MainMenu.Selected, typed);

            Assert.Equal(7, form.Preview().PopulationSize);
            Assert.Equal(new Settings().PopulationSize, menu.PopulationSize);
        }

        [Fact]
        public void ARequiredSettingTakesTheMenusValue()
        {
            var settings = new Settings { MaxEvaluations = 500 };

            Assert.Equal("500", Form(CommandRegistry.Get<ReachCommand>(), settings, MainMenu.TargetOf).Text(SettingOptions.Evaluations.Option));
        }

        [Fact]
        public void TheShellLineQuotesWordsWithSpacesOrCommas() =>
            Assert.Equal("snp-evolution compose --task \"Compute n\" --target \"1,2\"", CommandForm.Shell(new[] { "compose", "--task", "Compute n", "--target", "1,2" }));
    }
}
