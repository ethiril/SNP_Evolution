using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;

namespace SnpEvolution.Cli
{
    // Reading the --name value options every command takes.
    internal static class CommandOptions
    {
        // The evolve options that were given, which also win over the advisor's suggestions.
        internal static void ApplyOptions(Settings settings, IReadOnlyDictionary<string, string> options)
        {
            settings.MaxGenerations = (int)Number(options, "generations", settings.MaxGenerations);
            settings.PopulationSize = (int)Number(options, "population", settings.PopulationSize);
            settings.MaxNeurons = (int)Number(options, "neurons", settings.MaxNeurons);
            settings.StagnationPatience = (int)Number(options, "patience", settings.StagnationPatience);
            settings.IterativeEvolution = Switch(options, "iterative", settings.IterativeEvolution);
            settings.Lexicase = Switch(options, "lexicase", settings.Lexicase);
            settings.HardwareProfile = HardwareProfileOption(options, settings.HardwareProfile);
            settings.Modules = Switch(options, "modules", settings.Modules);
            settings.FreezeModules = Switch(options, "freeze", settings.FreezeModules);
            settings.TriggeredModules = Switch(options, "triggered", settings.TriggeredModules);
            settings.ModuleIncubation = (int)Number(options, "incubate", settings.ModuleIncubation);
            settings.MaxEvaluations = Number(options, "evaluations", settings.MaxEvaluations);
            settings.PartLibraryFolder = options.GetValueOrDefault("library", settings.PartLibraryFolder);
            settings.HandBuiltParts = Switch(options, "hand-built", settings.HandBuiltParts);
            settings.HandBuiltAddLoop = !LeavesOnly(options) && settings.HandBuiltAddLoop;
            settings.ProposeParts = Switch(options, "propose", settings.ProposeParts);
            settings.ProposalBudget = Number(options, "proposal-budget", settings.ProposalBudget);
            settings.Composition = settings.Composition with
            {
                MaxParts = (int)Number(options, "max-parts", settings.Composition.MaxParts),
                MaxGlue = (int)Number(options, "glue", settings.Composition.MaxGlue),
                GlueEdits = options.TryGetValue("glue-weight", out string? weight) && InputParsing.TryNonNegativeDouble(weight, out double glue)
                    ? glue
                    : settings.Composition.GlueEdits,
            };
            if (options.GetValueOrDefault("module-files") is string files)
            {
                settings.ModuleFiles = files.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                settings.Modules = true;
            }
            if (options.GetValueOrDefault("algorithm") is string algorithm)
            {
                settings.Algorithm = Catalog.Algorithms.FirstOrDefault(entry => entry.Name.Contains(algorithm, StringComparison.OrdinalIgnoreCase)) ?? settings.Algorithm;
            }
        }

        // The saved parts in the folder, with the hand-built parts when --hand-built asks for them.
        internal static ModuleLibrary PartLibrary(IReadOnlyDictionary<string, string> options, string folder)
        {
            ModuleLibrary library = PartLibraryFiles.Load(folder);
            if (IsOn(options, "hand-built"))
            {
                HandBuiltMachines.AddParts(library, _ => { }, addLoop: !LeavesOnly(options));
            }
            return library;
        }

        // --hand-built leaves leaves out the promoted add loop, as a control.
        internal static bool LeavesOnly(IReadOnlyDictionary<string, string> options) =>
            string.Equals(options.GetValueOrDefault("hand-built"), "leaves", StringComparison.OrdinalIgnoreCase);

        // --profile hardware keeps rules to threshold-and-reset forms; --profile none lifts it.
        internal static bool HardwareProfileOption(IReadOnlyDictionary<string, string> options, bool fallback) =>
            options.GetValueOrDefault("profile") is string profile ? string.Equals(profile, "hardware", StringComparison.OrdinalIgnoreCase) : fallback;

        internal static bool Switch(IReadOnlyDictionary<string, string> options, string name, bool fallback) =>
            options.ContainsKey(name) ? IsOn(options, name) : fallback;

        internal static bool IsOn(IReadOnlyDictionary<string, string> options, string name) =>
            options.GetValueOrDefault(name) is string value && !string.Equals(value, "off", StringComparison.OrdinalIgnoreCase) && value != "0";

        internal static Func<ISimulationEngine> Engine(IReadOnlyDictionary<string, string> options)
        {
            if (string.Equals(options.GetValueOrDefault("engine"), "sampled", StringComparison.OrdinalIgnoreCase))
            {
                return () => new SequentialCpuEngine();
            }
            int maxConfigurations = (int)Number(options, "configurations", ExhaustiveCpuEngine.DefaultMaxConfigurations);
            return () => new ExhaustiveCpuEngine(maxConfigurations);
        }

        internal static long Number(IReadOnlyDictionary<string, string> options, string name, long fallback) =>
            options.TryGetValue(name, out string? value) && long.TryParse(value, out long number) && number > 0 ? number : fallback;

        internal static List<T> Matching<T>(IEnumerable<T> items, Func<T, string> name, string? filter)
        {
            List<T> containing = items.Where(item => filter == null || name(item).Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            List<T> exact = containing.Where(item => string.Equals(name(item), filter, StringComparison.OrdinalIgnoreCase)).ToList();
            return exact.Count > 0 ? exact : containing;
        }
    }
}
