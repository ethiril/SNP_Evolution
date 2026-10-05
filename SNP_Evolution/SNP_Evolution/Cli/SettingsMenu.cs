using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Cli
{
    internal static class SettingsMenu
    {
        private const string NotPositiveInteger = "Number was not a positive integer.";

        private static readonly string[] Options =
        {
            "Max Steps", "Step-Through Amount", "GA Population Size", "Mutation Rate", "Max Generations", "Expected Set", "Experimental Rules",
            "Simulation Engine", "Fitness Function", "Genetic Algorithm", "Default Config",
        };

        // Returns the settings to use from now on, which is a fresh instance when defaults are restored.
        public static Settings Edit(Settings settings)
        {
            switch (ConsoleUi.Choose(settings, "", Options))
            {
                case 0:
                    PromptFor<int>("Please provide the maximum steps amount", NotPositiveInteger, InputParsing.TryPositiveInt, value => settings.MaxSteps = value);
                    break;
                case 1:
                    PromptFor<int>("Please provide the step-through amount", NotPositiveInteger, InputParsing.TryPositiveInt, value => settings.Repetitions = value);
                    break;
                case 2:
                    PromptFor<int>("Please provide the GA Population size", NotPositiveInteger, InputParsing.TryPositiveInt, value => settings.PopulationSize = value);
                    break;
                case 3:
                    PromptFor<float>("Please provide the mutation rate between 0 and 1", "Number was not a rate between 0 and 1.", InputParsing.TryProbability, value => settings.MutationRate = value);
                    break;
                case 4:
                    PromptFor<int>("Please provide the maximum generations amount", NotPositiveInteger, InputParsing.TryPositiveInt, value => settings.MaxGenerations = value);
                    break;
                case 5:
                    PromptFor<List<int>>("Please provide the list of integers separated by a comma that you wish to use as your expected set",
                        "A number in the set was not an integer.", InputParsing.TryIntegerSet, values => settings.ExpectedSet = values);
                    break;
                case 6:
                    if (ConsoleUi.Choose(settings, "Please select which rule setting you would like to use: ", new[] { "DISABLED", "ENABLED" }) is int ruleChoice)
                    {
                        settings.ExperimentalRules = ruleChoice == 1;
                    }
                    break;
                case 7:
                    settings.Engine = ChooseEntry(settings, "Please select which simulation engine to run networks on: ", Catalog.Engines) ?? settings.Engine;
                    break;
                case 8:
                    settings.FitnessFunction = ChooseEntry(settings, "Please select which fitness function to score networks with: ", Catalog.FitnessFunctions) ?? settings.FitnessFunction;
                    break;
                case 9:
                    settings.Algorithm = ChooseEntry(settings, "Please select which genetic algorithm to evolve networks with: ", Catalog.Algorithms) ?? settings.Algorithm;
                    break;
                case 10:
                    if (ConsoleUi.Choose(settings, "Please confirm whether to load default values for the configuration: ", new[] { "YES", "NO" }) == 0)
                    {
                        return Settings.Defaults();
                    }
                    break;
            }
            return settings;
        }

        // Returns null when the user presses ESC.
        private static CatalogEntry<TContext, T>? ChooseEntry<TContext, T>(Settings settings, string message, IReadOnlyList<CatalogEntry<TContext, T>> entries) =>
            ConsoleUi.Choose(settings, message, entries.Select(entry => entry.Name).ToList()) is int choice ? entries[choice] : null;

        private static void PromptFor<T>(string request, string invalidMessage, InputParser<T> parse, Action<T> assign) =>
            ConsoleUi.PromptUntilAccepted(request, invalidMessage, input =>
            {
                if (!parse(input, out T value))
                {
                    return false;
                }
                assign(value);
                return true;
            });
    }
}
