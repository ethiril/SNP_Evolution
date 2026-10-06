using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Simulation;
using static SnpEvolution.Cli.MenuPrompts;

namespace SnpEvolution.Cli
{
    // How the search copes with long targets and dead ends, and the module library it starts from.
    internal static class SearchMenu
    {
        // How the search copes with long targets and dead ends, and the limits on what a network can express.
        internal static void EditSearch(Settings settings)
        {
            const string NotNonNegativeInteger = "Number was not a whole number of 0 or more.";
            int selection = 0;
            while (ConsoleUi.Choose(settings, "Settings > Search", new[]
                {
                    ConsoleUi.Row("Iterative evolution", settings.IterativeEvolution ? "on" : "off"),
                    ConsoleUi.Row("First stage length", Automatic(settings.IterativeStartLength)),
                    ConsoleUi.Row("Values added per stage", Automatic(settings.IterativeStep)),
                    ConsoleUi.Row("Stagnation recovery", settings.StagnationRecovery ? "on" : "off"),
                    ConsoleUi.Row("Stagnation patience", settings.StagnationPatience),
                    ConsoleUi.Row("Max delay", settings.MaxDelay),
                    ConsoleUi.Row("Max spikes produced", settings.MaxProduce),
                    ConsoleUi.Row("Max initial spikes", settings.MaxInitialSpikes),
                    ConsoleUi.Row("Duplicate neurons", settings.DuplicateNeurons ? "on" : "off"),
                    ConsoleUi.Row("Lexicase parents", settings.Lexicase ? "on" : "off"),
                    ConsoleUi.Row("Build from modules", settings.Modules ? "on" : "off"),
                    ConsoleUi.Row("Freeze modules", settings.FreezeModules ? "on" : "off"),
                    ConsoleUi.Row("Triggered modules", settings.TriggeredModules ? "on" : "off"),
                    ConsoleUi.Row("Module incubation", settings.ModuleIncubation),
                    ConsoleUi.Row("Module files", settings.ModuleFiles.Count == 0 ? "none" : string.Join(", ", settings.ModuleFiles.Select(System.IO.Path.GetFileName))),
                    ConsoleUi.Row("Evaluation budget", settings.MaxEvaluations > 0 ? settings.MaxEvaluations.ToString() : "none"),
                    ConsoleUi.Row("Part library folder", settings.PartLibraryFolder),
                    ConsoleUi.Row("Composition: most part copies", settings.Composition.MaxParts),
                    ConsoleUi.Row("Composition: glue edit weight", settings.Composition.GlueEdits),
                    ConsoleUi.Row("Composition: most glue neurons", settings.Composition.MaxGlue > 0 ? settings.Composition.MaxGlue.ToString() : "max neurons"),
                    ConsoleUi.Row("Composition: propose parts when stalled", settings.ProposeParts ? "on" : "off"),
                    ConsoleUi.Row("Composition: evaluations per proposed part", settings.ProposalBudget),
                    ConsoleUi.Row("Composition: start from hand-built parts", settings.HandBuiltParts ? "on" : "off"),
                }, selection) is int choice)
            {
                selection = choice;
                switch (choice)
                {
                    case 0:
                        settings.IterativeEvolution = !settings.IterativeEvolution;
                        break;
                    case 1:
                        PromptFor<int>("Values in the first stage, or 0 for automatic", NotNonNegativeInteger, InputParsing.TryNonNegativeInt, value => settings.IterativeStartLength = value);
                        break;
                    case 2:
                        PromptFor<int>("Values each later stage adds, or 0 for automatic", NotNonNegativeInteger, InputParsing.TryNonNegativeInt, value => settings.IterativeStep = value);
                        break;
                    case 3:
                        settings.StagnationRecovery = !settings.StagnationRecovery;
                        break;
                    case 4:
                        PromptFor<int>("Generations without improvement before reacting", NotPositiveInteger, InputParsing.TryPositiveInt, value => settings.StagnationPatience = value);
                        break;
                    case 5:
                        PromptFor<int>("Longest delay a rule can have", NotNonNegativeInteger, InputParsing.TryNonNegativeInt, value => settings.MaxDelay = value);
                        break;
                    case 6:
                        PromptFor<int>("Most spikes a rule can send at once", NotPositiveInteger, InputParsing.TryPositiveInt, value => settings.MaxProduce = value);
                        break;
                    case 7:
                        PromptFor<int>("Most spikes a new neuron can start with", NotNonNegativeInteger, InputParsing.TryNonNegativeInt, value => settings.MaxInitialSpikes = value);
                        break;
                    case 8:
                        settings.DuplicateNeurons = !settings.DuplicateNeurons;
                        break;
                    case 9:
                        settings.Lexicase = !settings.Lexicase;
                        break;
                    case 10:
                        settings.Modules = !settings.Modules;
                        break;
                    case 11:
                        settings.FreezeModules = !settings.FreezeModules;
                        break;
                    case 12:
                        settings.TriggeredModules = !settings.TriggeredModules;
                        break;
                    case 13:
                        PromptFor<int>("Generations networks given a new module evolve apart, or 0 for none", NotNonNegativeInteger, InputParsing.TryNonNegativeInt, value => settings.ModuleIncubation = value);
                        break;
                    case 14:
                        EditModuleFiles(settings);
                        break;
                    case 15:
                        PromptFor<long>("Evaluations a run may spend, side runs and retests included, or 0 for no limit", "Number was not a whole number of 0 or more.",
                            InputParsing.TryNonNegativeLong, value => settings.MaxEvaluations = value);
                        break;
                    case 16:
                        ConsoleUi.PromptUntilAccepted("Folder of saved parts that composition search builds from", "Give a folder.", input =>
                        {
                            if (string.IsNullOrWhiteSpace(input))
                            {
                                return false;
                            }
                            settings.PartLibraryFolder = input.Trim();
                            return true;
                        }, "evolve-parts saves its parts here.");
                        break;
                    case 17:
                        PromptFor<int>("Most part copies a composed network may hold", NotPositiveInteger, InputParsing.TryPositiveInt,
                            value => settings.Composition = settings.Composition with { MaxParts = value });
                        break;
                    case 18:
                        PromptFor<double>("Weight of glue edits against part edits (1 is the default)", "Number was not 0 or more.", InputParsing.TryNonNegativeDouble,
                            value => settings.Composition = settings.Composition with { GlueEdits = value });
                        break;
                    case 19:
                        PromptFor<int>("Most glue neurons a composed network may hold, or 0 for the max neurons setting", NotNonNegativeInteger, InputParsing.TryNonNegativeInt,
                            value => settings.Composition = settings.Composition with { MaxGlue = value });
                        break;
                    case 20:
                        settings.ProposeParts = !settings.ProposeParts;
                        break;
                    case 21:
                        PromptFor<long>("Evaluations to spend evolving each proposed part", NotPositiveInteger, InputParsing.TryPositiveLong,
                            value => settings.ProposalBudget = value);
                        break;
                    case 22:
                        settings.HandBuiltParts = !settings.HandBuiltParts;
                        break;
                }
            }
        }

        // Saved networks to start the module library with; giving any turns building from modules on.
        private static void EditModuleFiles(Settings settings)
        {
            ConsoleUi.PromptUntilAccepted("Network files to start the module library with, separated by commas", "Could not load a network from every file.", input =>
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
