using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;
using SnpEvolution.Search.Benchmarking;
using static SnpEvolution.Cli.MenuPrompts;

namespace SnpEvolution.Cli
{
    // Settings grouped by what they affect, each group a page of its settings. A command's form shows the same groups,
    // holding the settings it takes.
    internal static class SettingsMenu
    {
        public static readonly IReadOnlyList<(string Name, IReadOnlyList<SettingOption> Options)> Groups = new (string, IReadOnlyList<SettingOption>)[]
        {
            ("Evolution", new SettingOption[]
            {
                SettingOptions.Fitness, SettingOptions.Algorithm, SettingOptions.Population, SettingOptions.MutationRate, SettingOptions.Generations,
                SettingOptions.Neurons, SettingOptions.Experimental,
            }),
            ("Search", new SettingOption[]
            {
                SettingOptions.Iterative, SettingOptions.FirstStage, SettingOptions.StageStep, SettingOptions.Recovery, SettingOptions.Patience,
                SettingOptions.MaxDelay, SettingOptions.MaxProduce, SettingOptions.InitialSpikes, SettingOptions.Duplicates, SettingOptions.Lexicase,
                SettingOptions.Modules, SettingOptions.Freeze, SettingOptions.Triggered, SettingOptions.Incubation, SettingOptions.ModuleFiles,
                SettingOptions.Evaluations, SettingOptions.Library, SettingOptions.PartBudget, SettingOptions.MaxParts, SettingOptions.GlueWeight,
                SettingOptions.Glue, SettingOptions.Propose, SettingOptions.ProposalBudget, SettingOptions.HandBuilt,
            }),
            ("Simulation", new SettingOption[]
            {
                SettingOptions.Simulator, SettingOptions.Steps, SettingOptions.Repetitions, SettingOptions.Rules, SettingOptions.Timing, SettingOptions.HardwareProfile,
            }),
            ("Benchmark", new SettingOption[] { SettingOptions.BenchmarkSeeds, SettingOptions.BenchmarkBudget, SettingOptions.BenchmarkPopulation }),
        };

        // What to evolve for comes first; the commands take it as --target or --task.
        private static readonly SettingRow[] Task =
        {
            new SettingRow(settings => ConsoleUi.Row("Task", settings.Task.Name), ChooseTask),
            new SettingRow(settings => ConsoleUi.Row("Target", $"{settings.Target.Kind} {settings.Target}"), settings => TargetMenu.EditTarget(settings)),
        };

        // Returns the settings to use from now on, which is a fresh instance when defaults are restored.
        public static Settings Edit(Settings settings)
        {
            int selection = 0;
            IEnumerable<string> pages = Groups.Select(group => group.Name + " >").Append("Reset to defaults");
            while (ConsoleUi.Choose(settings, "Settings", pages.ToList(), selection) is int choice)
            {
                selection = choice;
                if (choice < Groups.Count)
                {
                    IEnumerable<SettingRow> rows = Groups[choice].Options.Select(option => (SettingRow)option);
                    SettingsPage.Edit(settings, $"Settings > {Groups[choice].Name}", (choice == 0 ? Task.Concat(rows) : rows).ToList());
                }
                else if (ConsoleUi.Confirm(settings, "Load the default configuration?"))
                {
                    settings = Settings.Defaults();
                }
            }
            return settings;
        }

        // Saved networks to start the module library with; giving any turns building from modules on.
        internal static void EditModuleFiles(Settings settings)
        {
            ConsoleInput.PromptUntilAccepted("Network files to start the module library with, separated by commas", "Could not load a network from every file.", input =>
            {
                string[] files = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (files.Any(file => Storage.NetworkFiles.Load(file) == null))
                {
                    return false;
                }
                settings.ModuleFiles = files;
                settings.Modules |= files.Length > 0;
                return true;
            }, "Saved .json networks, such as the best network of an earlier run.", "Leave it empty to start with an empty library, so every module is found by the run.",
                "Giving any turns Build from modules on.");
        }

        // Suite tasks come with the rule form and timing they are meant for, which can still be changed after.
        private static void ChooseTask(Settings settings)
        {
            CatalogEntry<Settings, BenchmarkTask> task = ChooseEntry(settings, "Evolve networks for:", Catalog.Tasks, settings.Task);
            if (task != settings.Task)
            {
                settings.UseTask(task);
            }
        }
    }
}
