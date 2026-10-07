using SnpEvolution.Application;
using SnpEvolution.Search.Benchmarking;
using static SnpEvolution.Cli.MenuPrompts;

namespace SnpEvolution.Cli
{
    // Settings grouped by what they affect, each group a page of its settings.
    internal static class SettingsMenu
    {
        private static readonly SettingRow[] Evolution =
        {
            new SettingRow(settings => ConsoleUi.Row("Task", settings.Task.Name), ChooseTask),
            new SettingRow(settings => ConsoleUi.Row("Target", $"{settings.Target.Kind} {settings.Target}"), settings => TargetMenu.EditTarget(settings)),
            new SettingRow(settings => ConsoleUi.Row("Fitness function (sets)", settings.FitnessFunction.Name),
                settings => settings.FitnessFunction = ChooseEntry(settings, "Score set targets with:", Catalog.FitnessFunctions, settings.FitnessFunction)),
            SettingOptions.Algorithm,
            SettingOptions.Population,
            new SettingRow(settings => ConsoleUi.Row("Mutation rate", settings.MutationRate),
                settings => PromptFor<float>("Mutation rate, between 0 and 1", ValueKinds.Probability.Invalid, InputParsing.TryProbability, value => settings.MutationRate = value)),
            SettingOptions.Generations,
            SettingOptions.Neurons,
            SettingRow.Toggle("Experimental rules", settings => settings.ExperimentalRules, (settings, value) => settings.ExperimentalRules = value),
        };

        private static readonly SettingRow[] Simulation =
        {
            new SettingRow(settings => ConsoleUi.Row("Engine", settings.Engine.Name), settings => settings.Engine = ChooseEntry(settings, "Run networks on:", Catalog.Engines, settings.Engine)),
            new SettingRow(settings => ConsoleUi.Row("Max steps per run", settings.MaxSteps),
                settings => PromptFor<int>("Maximum steps per run", NotPositiveInteger, InputParsing.TryPositiveInt, value => settings.MaxSteps = value)),
            SettingOptions.Repetitions,
            new SettingRow(settings => ConsoleUi.Row("Rule form", settings.RuleForm), settings => settings.RuleForm = ChooseEnum(settings, "Form of the rules evolution creates:", settings.RuleForm)),
            new SettingRow(settings => ConsoleUi.Row("Output timing", settings.OutputTiming),
                settings => settings.OutputTiming = ChooseEnum(settings, "How the output neuron's spikes become a number:", settings.OutputTiming)),
            SettingOptions.HardwareProfile,
        };

        private static readonly SettingRow[] Benchmarks = { SettingOptions.BenchmarkSeeds, SettingOptions.BenchmarkBudget, SettingOptions.BenchmarkPopulation };

        // Returns the settings to use from now on, which is a fresh instance when defaults are restored.
        public static Settings Edit(Settings settings)
        {
            int selection = 0;
            while (ConsoleUi.Choose(settings, "Settings", new[] { "Evolution >", "Search >", "Simulation >", "Benchmarks >", "Reset to defaults" }, selection) is int choice)
            {
                selection = choice;
                switch (choice)
                {
                    case 0:
                        SettingsPage.Edit(settings, "Settings > Evolution", Evolution);
                        break;
                    case 1:
                        SearchMenu.EditSearch(settings);
                        break;
                    case 2:
                        SettingsPage.Edit(settings, "Settings > Simulation", Simulation);
                        break;
                    case 3:
                        SettingsPage.Edit(settings, "Settings > Benchmarks", Benchmarks);
                        break;
                    case 4:
                        if (ConsoleUi.Confirm(settings, "Load the default configuration?"))
                        {
                            settings = Settings.Defaults();
                        }
                        break;
                }
            }
            return settings;
        }

        // Suite tasks come with the rule form and timing they are meant for, which can still be changed after.
        private static void ChooseTask(Settings settings)
        {
            CatalogEntry<Settings, BenchmarkTask> task = ChooseEntry(settings, "Evolve networks for:", Catalog.Tasks, settings.Task);
            if (task == settings.Task)
            {
                return;
            }
            settings.Task = task;
            if (task != Catalog.TargetTask)
            {
                BenchmarkTask chosen = task.Create(settings);
                (settings.RuleForm, settings.OutputTiming) = (chosen.RuleForm, chosen.Timing);
            }
        }
    }
}
