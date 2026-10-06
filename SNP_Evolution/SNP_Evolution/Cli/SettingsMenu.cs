using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Simulation;
using static SnpEvolution.Cli.MenuPrompts;
using static SnpEvolution.Cli.SearchMenu;
using static SnpEvolution.Cli.TargetMenu;

namespace SnpEvolution.Cli
{
    // Settings grouped by what they affect. Each group lists its settings with their current values, and stays open
    // until the user goes back, so several can be changed in one visit.
    internal static class SettingsMenu
    {
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
    }
}
