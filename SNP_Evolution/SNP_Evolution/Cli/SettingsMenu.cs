using System;
using System.Collections.Generic;

namespace SnpEvolution.Cli
{
    internal static class SettingsMenu
    {
        private const string NotPositiveInteger = "Number was not a positive integer.";

        private static readonly string[] Options =
        {
            "Max Steps", "Step-Through Amount", "GA Population Size", "Mutation Rate", "Max Generations", "Expected Set", "Experimental Rules", "Default Config",
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
                    if (ConsoleUi.Choose(settings, "Please confirm whether to load default values for the configuration: ", new[] { "YES", "NO" }) == 0)
                    {
                        return Settings.Defaults();
                    }
                    break;
            }
            return settings;
        }

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
