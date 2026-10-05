using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Cli
{
    // Settings grouped by what they affect. Each group lists its settings with their current values, and stays open
    // until the user goes back, so several can be changed in one visit.
    internal static class SettingsMenu
    {
        private const string NotPositiveInteger = "Number was not a positive integer.";

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
                        EditEvolution(settings);
                        break;
                    case 1:
                        EditSearch(settings);
                        break;
                    case 2:
                        EditSimulation(settings);
                        break;
                    case 3:
                        EditBenchmarks(settings);
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

        // Asks for the kind of target and then its values, and makes matching it the task; false when the user backs
        // out. Leaving the values empty keeps the current ones, if they are of the chosen kind.
        public static bool EditTarget(Settings settings)
        {
            TargetKind[] kinds = Enum.GetValues<TargetKind>();
            string[] labels =
            {
                "Set: the numbers a run can produce, e.g. {2,4,6,8}",
                "Sequence: gaps between output spikes, in order, e.g. 1,1,2,3,5,8,13",
                "Binary word: the spike train step by step, e.g. 0110100110010110",
            };
            if (ConsoleUi.Choose(settings, "What kind of output should the system produce?", labels, Array.IndexOf(kinds, settings.Target.Kind)) is not int choice)
            {
                return false;
            }
            TargetKind kind = kinds[choice];
            bool accepted = false;
            string example = kind == TargetKind.BinaryWord ? "0110100110010110" : kind == TargetKind.Sequence ? "1,1,2,3,5,8,13" : "2,4,6,8";
            var notes = new List<string> { $"For example: {example}" };
            if (settings.Target.Kind == kind)
            {
                notes.Add($"Leave it empty to keep {settings.Target}.");
            }
            string invalid = kind == TargetKind.BinaryWord
                ? "Use only 0s and 1s, with at least one 1."
                : "Use positive whole numbers separated by commas or spaces.";
            ConsoleUi.PromptUntilAccepted($"Enter the {labels[choice].Split(':')[0].ToLowerInvariant()} to match", invalid, input =>
            {
                if (input.Trim().Length == 0 && settings.Target.Kind == kind)
                {
                    return accepted = true;
                }
                if (!OutputTarget.TryParse(kind, input, out OutputTarget target))
                {
                    return false;
                }
                settings.Target = target;
                return accepted = true;
            }, notes.ToArray());
            if (accepted)
            {
                settings.Task = Catalog.TargetTask;
            }
            return accepted;
        }

        // Asks how many generations to evolve a target for; false when the user backs out. Empty input means 1000.
        public static bool EditTargetGenerations(Settings settings)
        {
            const int DefaultGenerations = 1000;
            bool accepted = false;
            ConsoleUi.PromptUntilAccepted("Maximum number of generations", NotPositiveInteger, input =>
            {
                if (input.Trim().Length == 0)
                {
                    settings.MaxGenerations = DefaultGenerations;
                    return accepted = true;
                }
                if (!InputParsing.TryPositiveInt(input.Trim(), out int generations))
                {
                    return false;
                }
                settings.MaxGenerations = generations;
                return accepted = true;
            }, $"Target: {settings.Target.Kind} {settings.Target}", $"Leave it empty for {DefaultGenerations}. Evolution stops early once the target is matched.");
            return accepted;
        }

        private static void EditEvolution(Settings settings)
        {
            int selection = 0;
            while (ConsoleUi.Choose(settings, "Settings > Evolution", new[]
                {
                    ConsoleUi.Row("Task", settings.Task.Name),
                    ConsoleUi.Row("Target", $"{settings.Target.Kind} {settings.Target}"),
                    ConsoleUi.Row("Fitness function (sets)", settings.FitnessFunction.Name),
                    ConsoleUi.Row("Genetic algorithm", settings.Algorithm.Name),
                    ConsoleUi.Row("Population size", settings.PopulationSize),
                    ConsoleUi.Row("Mutation rate", settings.MutationRate),
                    ConsoleUi.Row("Max generations", settings.MaxGenerations),
                    ConsoleUi.Row("Max neurons", settings.MaxNeurons),
                    ConsoleUi.Row("Experimental rules", settings.ExperimentalRules ? "on" : "off"),
                }, selection) is int choice)
            {
                selection = choice;
                switch (choice)
                {
                    case 0:
                        ChooseTask(settings);
                        break;
                    case 1:
                        EditTarget(settings);
                        break;
                    case 2:
                        settings.FitnessFunction = ChooseEntry(settings, "Score set targets with:", Catalog.FitnessFunctions, settings.FitnessFunction);
                        break;
                    case 3:
                        settings.Algorithm = ChooseEntry(settings, "Evolve networks with:", Catalog.Algorithms, settings.Algorithm);
                        break;
                    case 4:
                        PromptFor<int>("Population size", NotPositiveInteger, InputParsing.TryPositiveInt, value => settings.PopulationSize = value);
                        break;
                    case 5:
                        PromptFor<float>("Mutation rate, between 0 and 1", "Number was not a rate between 0 and 1.", InputParsing.TryProbability, value => settings.MutationRate = value);
                        break;
                    case 6:
                        PromptFor<int>("Maximum number of generations", NotPositiveInteger, InputParsing.TryPositiveInt, value => settings.MaxGenerations = value);
                        break;
                    case 7:
                        PromptFor<int>("Maximum number of neurons in an evolved network", NotPositiveInteger, InputParsing.TryPositiveInt, value => settings.MaxNeurons = value);
                        break;
                    case 8:
                        settings.ExperimentalRules = !settings.ExperimentalRules;
                        break;
                }
            }
        }

        // How the search copes with long targets and dead ends, and the limits on what a network can express.
        private static void EditSearch(Settings settings)
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
                }
            }
        }

        private static string Automatic(int value) => value > 0 ? value.ToString() : "automatic";

        private static void EditSimulation(Settings settings)
        {
            int selection = 0;
            while (ConsoleUi.Choose(settings, "Settings > Simulation", new[]
                {
                    ConsoleUi.Row("Engine", settings.Engine.Name),
                    ConsoleUi.Row("Max steps per run", settings.MaxSteps),
                    ConsoleUi.Row("Runs per network", settings.Repetitions),
                    ConsoleUi.Row("Rule form", settings.RuleForm),
                    ConsoleUi.Row("Output timing", settings.OutputTiming),
                }, selection) is int choice)
            {
                selection = choice;
                switch (choice)
                {
                    case 0:
                        settings.Engine = ChooseEntry(settings, "Run networks on:", Catalog.Engines, settings.Engine);
                        break;
                    case 1:
                        PromptFor<int>("Maximum steps per run", NotPositiveInteger, InputParsing.TryPositiveInt, value => settings.MaxSteps = value);
                        break;
                    case 2:
                        PromptFor<int>("Number of runs per network", NotPositiveInteger, InputParsing.TryPositiveInt, value => settings.Repetitions = value);
                        break;
                    case 3:
                        settings.RuleForm = ChooseEnum(settings, "Form of the rules evolution creates:", settings.RuleForm);
                        break;
                    case 4:
                        settings.OutputTiming = ChooseEnum(settings, "How the output neuron's spikes become a number:", settings.OutputTiming);
                        break;
                }
            }
        }

        private static void EditBenchmarks(Settings settings)
        {
            int selection = 0;
            while (ConsoleUi.Choose(settings, "Settings > Benchmarks", new[]
                {
                    ConsoleUi.Row("Seeds per task", settings.BenchmarkSeeds),
                    ConsoleUi.Row("Evaluations per run", settings.EvaluationBudget),
                    ConsoleUi.Row("Population size", settings.BenchmarkPopulationSize),
                }, selection) is int choice)
            {
                selection = choice;
                switch (choice)
                {
                    case 0:
                        PromptFor<int>("Number of seeds each benchmark runs", NotPositiveInteger, InputParsing.TryPositiveInt, value => settings.BenchmarkSeeds = value);
                        break;
                    case 1:
                        PromptFor<int>("Number of network evaluations each benchmark run may use", NotPositiveInteger, InputParsing.TryPositiveInt, value => settings.EvaluationBudget = value);
                        break;
                    case 2:
                        PromptFor<int>("Population size for benchmarks", NotPositiveInteger, InputParsing.TryPositiveInt, value => settings.BenchmarkPopulationSize = value);
                        break;
                }
            }
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

        // Returns current when the user goes back.
        private static CatalogEntry<TContext, T> ChooseEntry<TContext, T>(Settings settings, string message, IReadOnlyList<CatalogEntry<TContext, T>> entries, CatalogEntry<TContext, T> current) =>
            ConsoleUi.Choose(settings, message, entries.Select(entry => entry.Name).ToList(), entries.ToList().IndexOf(current)) is int choice ? entries[choice] : current;

        private static TEnum ChooseEnum<TEnum>(Settings settings, string message, TEnum current) where TEnum : struct, Enum
        {
            TEnum[] values = Enum.GetValues<TEnum>();
            return ConsoleUi.Choose(settings, message, values.Select(value => value.ToString()).ToList(), Array.IndexOf(values, current)) is int choice ? values[choice] : current;
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
