using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;
using SnpEvolution.Search;
using SnpEvolution.Search.Genome;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Cli
{
    // Every setting, each with a flag and a menu row. Commands list the ones they take; the settings menus show them.
    internal static class SettingOptions
    {
        public static readonly SettingOption<EvolutionSearch> Algorithm = new(
            new Option<EvolutionSearch>("algorithm", ValueKinds.Named(Catalog.Algorithms, search => search.Name, "the name of a search; run 'algorithms' to list them"),
                "the search to evolve with; any whose name contains NAME", "NAME"), "Genetic algorithm",
            settings => settings.Algorithm, (settings, value) => settings.Algorithm = value,
            edit: settings => settings.Algorithm = MenuPrompts.Choose(settings, "Evolve networks with:", Catalog.Algorithms, settings.Algorithm, search => search.Name));

        public static readonly SettingOption<CatalogEntry<Settings, IFitnessFunction>> Fitness = new(
            new Option<CatalogEntry<Settings, IFitnessFunction>>("fitness", ValueKinds.Named(Catalog.FitnessFunctions, entry => entry.Name, "the name of a fitness function"),
                "how set targets are scored; any whose name contains NAME", "NAME"), "Fitness function (sets)",
            settings => settings.FitnessFunction, (settings, value) => settings.FitnessFunction = value,
            edit: settings => settings.FitnessFunction = MenuPrompts.ChooseEntry(settings, "Score set targets with:", Catalog.FitnessFunctions, settings.FitnessFunction));

        public static readonly SettingOption<float> MutationRate = new(new Option<float>("mutation-rate", ValueKinds.Probability, "the chance of each mutation, between 0 and 1"), "Mutation rate",
            settings => settings.MutationRate, (settings, value) => settings.MutationRate = value, "Mutation rate, between 0 and 1");

        public static readonly SettingOption<bool> Experimental = new(new Option<bool>("experimental", ValueKinds.Switch, "let evolution use the experimental rule expressions"), "Experimental rules",
            settings => settings.ExperimentalRules, (settings, value) => settings.ExperimentalRules = value);

        public static readonly SettingOption<int> Population = new(new Option<int>("population", ValueKinds.PositiveInt, "networks per generation"), "Population size",
            settings => settings.PopulationSize, (settings, value) => settings.PopulationSize = value);

        public static readonly SettingOption<int> Generations = new(new Option<int>("generations", ValueKinds.PositiveInt, "most generations to run"), "Max generations",
            settings => settings.MaxGenerations, (settings, value) => settings.MaxGenerations = value, "Maximum number of generations");

        public static readonly SettingOption<int> Neurons = new(new Option<int>("neurons", ValueKinds.PositiveInt, "most neurons an evolved network may have"), "Max neurons",
            settings => settings.MaxNeurons, (settings, value) => settings.MaxNeurons = value, "Maximum number of neurons in an evolved network");

        public static readonly SettingOption<int> FirstStage = new(new Option<int>("first-stage", ValueKinds.NonNegativeInt, "values in the first stage, or 0 for automatic"), "First stage length",
            settings => settings.IterativeStartLength, (settings, value) => settings.IterativeStartLength = value, "Values in the first stage, or 0 for automatic",
            show: settings => MenuPrompts.Automatic(settings.IterativeStartLength));

        public static readonly SettingOption<int> StageStep = new(new Option<int>("stage-step", ValueKinds.NonNegativeInt, "values each later stage adds, or 0 for automatic"), "Values added per stage",
            settings => settings.IterativeStep, (settings, value) => settings.IterativeStep = value, "Values each later stage adds, or 0 for automatic",
            show: settings => MenuPrompts.Automatic(settings.IterativeStep));

        public static readonly SettingOption<bool> Recovery = new(new Option<bool>("recovery", ValueKinds.Switch, "react when the best fitness stops improving"), "Stagnation recovery",
            settings => settings.StagnationRecovery, (settings, value) => settings.StagnationRecovery = value);

        public static readonly SettingOption<int> MaxDelay = new(new Option<int>("max-delay", ValueKinds.NonNegativeInt, "the longest delay a rule can have"), "Max delay",
            settings => settings.MaxDelay, (settings, value) => settings.MaxDelay = value, "Longest delay a rule can have");

        public static readonly SettingOption<int> MaxProduce = new(new Option<int>("max-produce", ValueKinds.PositiveInt, "the most spikes a rule can send at once"), "Max spikes produced",
            settings => settings.MaxProduce, (settings, value) => settings.MaxProduce = value, "Most spikes a rule can send at once");

        public static readonly SettingOption<int> InitialSpikes = new(new Option<int>("initial-spikes", ValueKinds.NonNegativeInt, "the most spikes a new neuron can start with"), "Max initial spikes",
            settings => settings.MaxInitialSpikes, (settings, value) => settings.MaxInitialSpikes = value, "Most spikes a new neuron can start with");

        public static readonly SettingOption<bool> Duplicates = new(new Option<bool>("duplicates", ValueKinds.Switch, "let mutation copy working neurons"), "Duplicate neurons",
            settings => settings.DuplicateNeurons, (settings, value) => settings.DuplicateNeurons = value);

        public static readonly SettingOption<bool> Iterative = new(new Option<bool>("iterative", ValueKinds.Switch, "evolve a long target a few values at a time"), "Iterative evolution",
            settings => settings.IterativeEvolution, (settings, value) => settings.IterativeEvolution = value);

        public static readonly SettingOption<int> Patience = new(new Option<int>("patience", ValueKinds.PositiveInt, "generations without improvement before reacting"), "Stagnation patience",
            settings => settings.StagnationPatience, (settings, value) => settings.StagnationPatience = value, "Generations without improvement before reacting");

        public static readonly SettingOption<bool> Lexicase = new(new Option<bool>("lexicase", ValueKinds.Switch, "pick parents by lexicase selection"), "Lexicase parents",
            settings => settings.Lexicase, (settings, value) => settings.Lexicase = value);

        public static readonly SettingOption<bool> Modules = new(new Option<bool>("modules", ValueKinds.Switch, "build from modules the run finds"), "Build from modules",
            settings => settings.Modules, (settings, value) => settings.Modules = value);

        public static readonly SettingOption<bool> Freeze = new(new Option<bool>("freeze", ValueKinds.Switch, "keep modules frozen"), "Freeze modules",
            settings => settings.FreezeModules, (settings, value) => settings.FreezeModules = value);

        public static readonly SettingOption<bool> Triggered = new(new Option<bool>("triggered", ValueKinds.Switch, "let side runs build parts that wait for a trigger"), "Triggered modules",
            settings => settings.TriggeredModules, (settings, value) => settings.TriggeredModules = value);

        public static readonly SettingOption<int> Incubation = new(new Option<int>("incubate", ValueKinds.NonNegativeInt, "generations networks with a new module evolve apart"), "Module incubation",
            settings => settings.ModuleIncubation, (settings, value) => settings.ModuleIncubation = value, "Generations networks given a new module evolve apart, or 0 for none");

        // Giving any files turns building from modules on; the menu checks every file holds a network.
        public static readonly SettingOption<string[]> ModuleFiles = new(new Option<string[]>("module-files", ValueKinds.List, "saved networks to start the module library with", "a.json,b.json"), "Module files",
            settings => settings.ModuleFiles.ToArray(), (settings, files) => (settings.ModuleFiles, settings.Modules) = (files, settings.Modules || files.Length > 0),
            show: settings => settings.ModuleFiles.Count == 0 ? "none" : string.Join(", ", settings.ModuleFiles.Select(System.IO.Path.GetFileName)),
            edit: SettingsMenu.EditModuleFiles);

        public static readonly SettingOption<long> Evaluations = new(new Option<long>("evaluations", ValueKinds.NonNegativeLong, "evaluations a run may spend in all, or 0 for no limit"), "Evaluation budget",
            settings => settings.MaxEvaluations, (settings, value) => settings.MaxEvaluations = value, "Evaluations a run may spend, side runs and retests included, or 0 for no limit",
            show: settings => settings.MaxEvaluations > 0 ? settings.MaxEvaluations.ToString() : "none");

        public static readonly SettingOption<string> Library = new(new Option<string>("library", ValueKinds.Text, "the part library folder", "DIR"), "Part library folder",
            settings => settings.PartLibraryFolder, (settings, value) => settings.PartLibraryFolder = value,
            edit: settings => ConsoleInput.PromptUntilAccepted("Folder of saved parts that composition search builds from", "Give a folder.", input =>
            {
                if (string.IsNullOrWhiteSpace(input))
                {
                    return false;
                }
                settings.PartLibraryFolder = input.Trim();
                return true;
            }, "evolve-parts saves its parts here."));

        public static readonly SettingOption<int> MaxParts = new(new Option<int>("max-parts", ValueKinds.PositiveInt, "most part copies a composed network may hold"), "Composition: most part copies",
            settings => settings.Composition.MaxParts, (settings, value) => settings.Composition = settings.Composition with { MaxParts = value }, "Most part copies a composed network may hold");

        public static readonly SettingOption<double> GlueWeight = new(new Option<double>("glue-weight", ValueKinds.NonNegativeDouble, "weight of glue edits against part edits"), "Composition: glue edit weight",
            settings => settings.Composition.GlueEdits, (settings, value) => settings.Composition = settings.Composition with { GlueEdits = value }, "Weight of glue edits against part edits (1 is the default)");

        public static readonly SettingOption<int> Glue = new(new Option<int>("glue", ValueKinds.NonNegativeInt, "most glue neurons, or 0 for --neurons"), "Composition: most glue neurons",
            settings => settings.Composition.MaxGlue, (settings, value) => settings.Composition = settings.Composition with { MaxGlue = value },
            "Most glue neurons a composed network may hold, or 0 for the max neurons setting", show: settings => settings.Composition.MaxGlue > 0 ? settings.Composition.MaxGlue.ToString() : "max neurons");

        public static readonly SettingOption<bool> Propose = new(new Option<bool>("propose", ValueKinds.Switch, "propose the parts a stalled composition run lacks"), "Composition: propose parts when stalled",
            settings => settings.ProposeParts, (settings, value) => settings.ProposeParts = value);

        public static readonly SettingOption<long> ProposalBudget = new(new Option<long>("proposal-budget", ValueKinds.PositiveLong, "evaluations to evolve each proposed part"), "Composition: evaluations per proposed part",
            settings => settings.ProposalBudget, (settings, value) => settings.ProposalBudget = value, "Evaluations to spend evolving each proposed part");

        // leaves leaves out the promoted add loop, as a control.
        public static readonly SettingOption<string> HandBuilt = new(new Option<string>("hand-built", ValueKinds.Choice("on", "off", "leaves"), "add the hand-built parts to the library"),
            "Composition: start from hand-built parts", settings => !settings.HandBuiltParts ? "off" : settings.HandBuiltAddLoop ? "on" : "leaves",
            (settings, value) => (settings.HandBuiltParts, settings.HandBuiltAddLoop) = (value != "off", value != "leaves"), "Add the hand-built parts to the library:");

        public static readonly SettingOption<long> PartBudget = new(new Option<long>("budget", ValueKinds.PositiveLong, "evaluations to search for each part"), "Evaluations per library part",
            settings => settings.PartBudget, (settings, value) => settings.PartBudget = value, "Evaluations evolve-parts spends searching for each part");

        public static readonly SettingOption<bool> HardwareProfile = new(new Option<bool>("profile", ProfileKind, "hardware keeps every rule to threshold-and-reset forms"), "Hardware profile",
            settings => settings.HardwareProfile, (settings, value) => settings.HardwareProfile = value, show: settings => settings.HardwareProfile ? "on (threshold-and-reset rules only)" : "off");

        public static readonly SettingOption<CatalogEntry<Settings, ISimulationEngine>> Simulator = new(
            new Option<CatalogEntry<Settings, ISimulationEngine>>("simulator", ValueKinds.Named(Catalog.Engines, entry => entry.Name, "the name of an engine"),
                "the engine that runs networks; any whose name contains NAME", "NAME"), "Engine",
            settings => settings.Engine, (settings, value) => settings.Engine = value,
            edit: settings => settings.Engine = MenuPrompts.ChooseEntry(settings, "Run networks on:", Catalog.Engines, settings.Engine));

        public static readonly SettingOption<int> Steps = new(new Option<int>("steps", ValueKinds.PositiveInt, "the most steps a run lasts"), "Max steps per run",
            settings => settings.MaxSteps, (settings, value) => settings.MaxSteps = value, "Maximum steps per run");

        public static readonly SettingOption<RuleForm> Rules = new(new Option<RuleForm>("rule-form", ValueKinds.Enum<RuleForm>(), "the form of the rules evolution creates"), "Rule form",
            settings => settings.RuleForm, (settings, value) => settings.RuleForm = value, "Form of the rules evolution creates:");

        public static readonly SettingOption<OutputTiming> Timing = new(new Option<OutputTiming>("timing", ValueKinds.Enum<OutputTiming>(), "how the output neuron's spikes become a number"), "Output timing",
            settings => settings.OutputTiming, (settings, value) => settings.OutputTiming = value, "How the output neuron's spikes become a number:");

        public static readonly SettingOption<int> Repetitions = new(new Option<int>("repetitions", ValueKinds.PositiveInt, "sampled runs that score each network"), "Runs per network",
            settings => settings.Repetitions, (settings, value) => settings.Repetitions = value, "Number of runs per network");

        public static readonly SettingOption<int> BenchmarkSeeds = new(new Option<int>("seeds", ValueKinds.PositiveInt, "seeds per task"), "Seeds per task",
            settings => settings.BenchmarkSeeds, (settings, value) => settings.BenchmarkSeeds = value, "Number of seeds each benchmark runs");

        public static readonly SettingOption<long> BenchmarkBudget = new(new Option<long>("budget", ValueKinds.PositiveLong, "evaluations per run"), "Evaluations per run",
            settings => settings.EvaluationBudget, (settings, value) => settings.EvaluationBudget = value, "Number of network evaluations each benchmark run may use");

        public static readonly SettingOption<int> BenchmarkPopulation = new(new Option<int>("population", ValueKinds.PositiveInt, "networks per generation"), "Population size",
            settings => settings.BenchmarkPopulationSize, (settings, value) => settings.BenchmarkPopulationSize = value, "Population size for benchmarks");

        // The settings that say how networks are run, which every command that runs them takes.
        public static readonly IReadOnlyList<SettingOption> Simulation = new SettingOption[] { Simulator, Steps, Repetitions, Timing };

        // The settings evolve, advise, reach and compose take, in the order they are applied: module files after modules.
        public static readonly IReadOnlyList<SettingOption> Evolve = new SettingOption[]
        {
            Generations, Population, Neurons, MutationRate, Experimental, Fitness, Patience, Recovery, Iterative, FirstStage, StageStep, MaxDelay, MaxProduce,
            InitialSpikes, Duplicates, Lexicase, HardwareProfile, Rules, Modules, Freeze, Triggered, Incubation, Evaluations, Library, HandBuilt, Propose,
            ProposalBudget, MaxParts, Glue, GlueWeight, ModuleFiles, Algorithm,
        }.Concat(Simulation).ToList();

        // Every setting option, so the menu can find the setting behind a command's option.
        public static readonly IReadOnlyList<SettingOption> All =
            Evolve.Concat(new SettingOption[] { PartBudget, BenchmarkSeeds, BenchmarkBudget, BenchmarkPopulation }).ToList();

        public static SettingOption? For(Option option) => All.FirstOrDefault(setting => setting.Option == option);

        public static void Apply(Settings settings, CommandArgs args, IEnumerable<SettingOption> options)
        {
            foreach (SettingOption option in options)
            {
                option.ApplyFrom(args, settings);
            }
        }

        public static IReadOnlyList<Option> Flags(IEnumerable<SettingOption> options) => options.Select(option => option.Option).ToList();

        private static ValueKind<bool> ProfileKind => ValueKinds.Words(("hardware", true), ("none", false));
    }
}
