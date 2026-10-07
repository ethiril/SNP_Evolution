using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;
using SnpEvolution.Evolution.Search;

namespace SnpEvolution.Cli
{
    // A setting that is both a command-line option and a row of the settings menu, declared once for both.
    internal abstract class SettingOption
    {
        protected SettingOption(string label)
        {
            Label = label;
        }

        // The menu row's label.
        public string Label { get; }

        public abstract Option Option { get; }

        public abstract string Row(Settings settings);

        // Sets the setting from the command line, when the option was given.
        public abstract void ApplyFrom(CommandArgs args, Settings settings);

        // Changes the setting from the menu: a switch flips, anything else is asked for.
        public abstract void Edit(Settings settings);
    }

    internal sealed class SettingOption<T> : SettingOption where T : notnull
    {
        private readonly Func<Settings, T> get;
        private readonly Action<Settings, T> set;
        private readonly Func<Settings, object>? show;
        private readonly string? prompt;
        private readonly Action<Settings>? edit;

        // prompt is what the menu asks; show is the row's value when it is not simply the setting; edit replaces the
        // menu's flip or prompt.
        public SettingOption(Option<T> option, string label, Func<Settings, T> get, Action<Settings, T> set,
            string? prompt = null, Func<Settings, object>? show = null, Action<Settings>? edit = null) : base(label)
        {
            Typed = option;
            this.get = get;
            this.set = set;
            this.prompt = prompt;
            this.show = show;
            this.edit = edit;
        }

        public Option<T> Typed { get; }

        public override Option Option => Typed;

        public override string Row(Settings settings) =>
            ConsoleUi.Row(Label, show?.Invoke(settings) ?? (get(settings) is bool on ? (on ? "on" : "off") : get(settings)));

        public override void ApplyFrom(CommandArgs args, Settings settings)
        {
            if (args.TryGet(Typed, out T value))
            {
                set(settings, value);
            }
        }

        public override void Edit(Settings settings)
        {
            if (edit != null)
            {
                edit(settings);
            }
            else if (get(settings) is bool on)
            {
                set(settings, (T)(object)!on);
            }
            else
            {
                MenuPrompts.PromptFor(prompt ?? Label, Typed.Kind.Invalid, Typed.Kind.Parse, value => set(settings, value));
            }
        }
    }

    // Every setting with both a flag and a menu row. Commands list the ones they take; the settings menus show them.
    internal static class SettingOptions
    {
        public static readonly SettingOption<EvolutionSearch> Algorithm = new(
            new Option<EvolutionSearch>("algorithm", AlgorithmKind, "the search to evolve with; any whose name contains NAME", "NAME"), "Genetic algorithm",
            settings => settings.Algorithm, (settings, value) => settings.Algorithm = value, show: settings => settings.Algorithm.Name,
            edit: settings => settings.Algorithm = MenuPrompts.Choose(settings, "Evolve networks with:", Catalog.Algorithms, settings.Algorithm, search => search.Name));

        public static readonly SettingOption<int> Population = new(new Option<int>("population", ValueKinds.PositiveInt, "networks per generation"), "Population size",
            settings => settings.PopulationSize, (settings, value) => settings.PopulationSize = value);

        public static readonly SettingOption<int> Generations = new(new Option<int>("generations", ValueKinds.PositiveInt, "most generations to run"), "Max generations",
            settings => settings.MaxGenerations, (settings, value) => settings.MaxGenerations = value, "Maximum number of generations");

        public static readonly SettingOption<int> Neurons = new(new Option<int>("neurons", ValueKinds.PositiveInt, "most neurons an evolved network may have"), "Max neurons",
            settings => settings.MaxNeurons, (settings, value) => settings.MaxNeurons = value, "Maximum number of neurons in an evolved network");

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
            edit: SearchMenu.EditModuleFiles);

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

        // leaves leaves out the promoted add loop, as a control; the menu only turns the hand-built parts on and off.
        public static readonly SettingOption<string> HandBuilt = new(new Option<string>("hand-built", ValueKinds.Choice("on", "off", "leaves"), "add the hand-built parts to the library"),
            "Composition: start from hand-built parts", settings => !settings.HandBuiltParts ? "off" : settings.HandBuiltAddLoop ? "on" : "leaves",
            (settings, value) => (settings.HandBuiltParts, settings.HandBuiltAddLoop) = (value != "off", value != "leaves"),
            show: settings => settings.HandBuiltParts ? "on" : "off", edit: settings => settings.HandBuiltParts = !settings.HandBuiltParts);

        public static readonly SettingOption<bool> HardwareProfile = new(new Option<bool>("profile", ProfileKind, "hardware keeps every rule to threshold-and-reset forms"), "Hardware profile",
            settings => settings.HardwareProfile, (settings, value) => settings.HardwareProfile = value, show: settings => settings.HardwareProfile ? "on (threshold-and-reset rules only)" : "off");

        public static readonly SettingOption<int> Repetitions = new(new Option<int>("repetitions", ValueKinds.PositiveInt, "sampled runs that score each network"), "Runs per network",
            settings => settings.Repetitions, (settings, value) => settings.Repetitions = value, "Number of runs per network");

        public static readonly SettingOption<int> BenchmarkSeeds = new(new Option<int>("seeds", ValueKinds.PositiveInt, "seeds per task"), "Seeds per task",
            settings => settings.BenchmarkSeeds, (settings, value) => settings.BenchmarkSeeds = value, "Number of seeds each benchmark runs");

        public static readonly SettingOption<long> BenchmarkBudget = new(new Option<long>("budget", ValueKinds.PositiveLong, "evaluations per run"), "Evaluations per run",
            settings => settings.EvaluationBudget, (settings, value) => settings.EvaluationBudget = value, "Number of network evaluations each benchmark run may use");

        public static readonly SettingOption<int> BenchmarkPopulation = new(new Option<int>("population", ValueKinds.PositiveInt, "networks per generation"), "Population size",
            settings => settings.BenchmarkPopulationSize, (settings, value) => settings.BenchmarkPopulationSize = value, "Population size for benchmarks");

        // The settings evolve, advise, reach and compose take, in the order they are applied: module files after modules.
        public static readonly IReadOnlyList<SettingOption> Evolve = new SettingOption[]
        {
            Generations, Population, Neurons, Patience, Iterative, Lexicase, HardwareProfile, Modules, Freeze, Triggered, Incubation, Evaluations,
            Library, HandBuilt, Propose, ProposalBudget, MaxParts, Glue, GlueWeight, ModuleFiles, Algorithm,
        };

        public static void Apply(Settings settings, CommandArgs args, IEnumerable<SettingOption> options)
        {
            foreach (SettingOption option in options)
            {
                option.ApplyFrom(args, settings);
            }
        }

        public static IReadOnlyList<Option> Flags(IEnumerable<SettingOption> options) => options.Select(option => option.Option).ToList();

        private static ValueKind<EvolutionSearch> AlgorithmKind => new ValueKind<EvolutionSearch>(
            (string input, out EvolutionSearch value) => (value = Catalog.Matching(Catalog.Algorithms, search => search.Name, input.Trim()).FirstOrDefault()!) != null,
            "the name of a search; run 'algorithms' to list them", "", "NAME");

        private static ValueKind<bool> ProfileKind => ValueKinds.Words(("hardware", true), ("none", false));
    }
}
