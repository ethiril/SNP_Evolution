using System;
using System.Linq;
using SnpEvolution.Application;
using static SnpEvolution.Cli.MenuPrompts;

namespace SnpEvolution.Cli
{
    // How the search copes with long targets and dead ends, the limits on what a network can express, and the module
    // and part libraries it builds from.
    internal static class SearchMenu
    {
        private static readonly SettingRow[] Rows =
        {
            SettingOptions.Iterative,
            new SettingRow(settings => ConsoleUi.Row("First stage length", Automatic(settings.IterativeStartLength)),
                settings => PromptFor<int>("Values in the first stage, or 0 for automatic", NotNonNegativeInteger, InputParsing.TryNonNegativeInt, value => settings.IterativeStartLength = value)),
            new SettingRow(settings => ConsoleUi.Row("Values added per stage", Automatic(settings.IterativeStep)),
                settings => PromptFor<int>("Values each later stage adds, or 0 for automatic", NotNonNegativeInteger, InputParsing.TryNonNegativeInt, value => settings.IterativeStep = value)),
            SettingRow.Toggle("Stagnation recovery", settings => settings.StagnationRecovery, (settings, value) => settings.StagnationRecovery = value),
            SettingOptions.Patience,
            new SettingRow(settings => ConsoleUi.Row("Max delay", settings.MaxDelay),
                settings => PromptFor<int>("Longest delay a rule can have", NotNonNegativeInteger, InputParsing.TryNonNegativeInt, value => settings.MaxDelay = value)),
            new SettingRow(settings => ConsoleUi.Row("Max spikes produced", settings.MaxProduce),
                settings => PromptFor<int>("Most spikes a rule can send at once", NotPositiveInteger, InputParsing.TryPositiveInt, value => settings.MaxProduce = value)),
            new SettingRow(settings => ConsoleUi.Row("Max initial spikes", settings.MaxInitialSpikes),
                settings => PromptFor<int>("Most spikes a new neuron can start with", NotNonNegativeInteger, InputParsing.TryNonNegativeInt, value => settings.MaxInitialSpikes = value)),
            SettingRow.Toggle("Duplicate neurons", settings => settings.DuplicateNeurons, (settings, value) => settings.DuplicateNeurons = value),
            SettingOptions.Lexicase,
            SettingOptions.Modules,
            SettingOptions.Freeze,
            SettingOptions.Triggered,
            SettingOptions.Incubation,
            SettingOptions.ModuleFiles,
            SettingOptions.Evaluations,
            SettingOptions.Library,
            SettingOptions.MaxParts,
            SettingOptions.GlueWeight,
            SettingOptions.Glue,
            SettingOptions.Propose,
            SettingOptions.ProposalBudget,
            SettingOptions.HandBuilt,
        };

        internal static void EditSearch(Settings settings) => SettingsPage.Edit(settings, "Settings > Search", Rows);

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
    }
}
