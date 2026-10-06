using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;
using static SnpEvolution.Cli.CommandLine;
using static SnpEvolution.Cli.CommandOptions;

namespace SnpEvolution.Cli
{
    // The commands that build library parts: evolve-parts evolves first parts and compose composes them for a suite task.
    internal static class PartCommands
    {
        // The seed is 1 and the budget the settings' part budget unless given; --only takes contract names, each matching any
        // contract whose name contains it, ignoring case. Exits with 2 when a contract is left without a part.
        internal static int EvolveParts(IReadOnlyDictionary<string, string> options, Settings settings)
        {
            List<Contract> contracts = FirstParts.Contracts.ToList();
            if (options.GetValueOrDefault("only") is string only)
            {
                string[] names = only.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (names.FirstOrDefault(name => !contracts.Any(contract => contract.Name.Contains(name, StringComparison.OrdinalIgnoreCase))) is string unknown)
                {
                    Console.Error.WriteLine($"No first-part contract matches '{unknown}'. The contracts are: {string.Join(", ", contracts.Select(contract => contract.Name))}.");
                    return 1;
                }
                contracts = contracts.Where(contract => names.Any(name => contract.Name.Contains(name, StringComparison.OrdinalIgnoreCase))).ToList();
            }
            string engine = options.GetValueOrDefault("engine") is string named ? $" --engine {named}" : "";
            var parts = new PartsSession.Options(
                (int)Number(options, "seed", 1),
                Number(options, "budget", settings.PartBudget),
                contracts,
                options.GetValueOrDefault("library", settings.PartLibraryFolder),
                Engine(options),
                IsOn(options, "redo"),
                engine,
                HardwareProfileOption(options, false),
                (int)Number(options, "robust", 0));
            return PartsSession.Run(parts, Console.WriteLine);
        }

        // Exits with 2 when the task was not solved, so a script can tell a failed search from a bad command.
        internal static int Compose(IReadOnlyDictionary<string, string> options)
        {
            if (ComposeSettings(options) is not Settings settings)
            {
                return 1;
            }
            var random = new Random((int)Number(options, "seed", 1));
            BenchmarkTask task = settings.SelectedTask;
            Console.WriteLine("Composing a network for {0} with {1}.", task.Name, settings.Algorithm.Name);
            EvolutionSession.Notes(settings, task).ToList().ForEach(Console.WriteLine);
            var evaluations = new EvaluationCounter();
            IGeneticAlgorithm run;
            try
            {
                run = EvolutionSession.Evolve(settings, task, factory => factory.NewNetwork(), random, Console.WriteLine, evaluations);
            }
            catch (InvalidDataException exception)
            {
                Console.Error.WriteLine(exception.Message);
                return 1;
            }
            RunOutput.Save(run, EvolutionSession.NewOutputFolder(), "ComposedNet", Console.WriteLine, evaluations);
            return EvolutionSession.IsSolved(run, task.Task) ? 0 : 2;
        }

        // Null, with the reason written to stderr, when the task or algorithm options name nothing compose can run.
        private static Settings? ComposeSettings(IReadOnlyDictionary<string, string> options)
        {
            string? name = options.GetValueOrDefault("task");
            List<CatalogEntry<Settings, BenchmarkTask>> matching = Matching(Catalog.Tasks.Skip(1), task => task.Name, name);
            if (name == null || matching.Count != 1)
            {
                Console.Error.WriteLine(Usage);
                Console.Error.WriteLine("compose needs a --task that names one task; run 'tasks' to list them.");
                return null;
            }
            BenchmarkTask suiteTask = matching[0].Create(new Settings());
            var settings = new Settings
            {
                Task = matching[0],
                RuleForm = suiteTask.RuleForm,
                OutputTiming = suiteTask.Timing,
                Algorithm = Catalog.Algorithms.First(entry => AlgorithmCatalog.IsComposition(entry.Name) && entry.Name.Contains("tournament")),
                MaxEvaluations = 30_000,
                MaxGenerations = 5_000,
                Lexicase = true,
            };
            ApplyOptions(settings, options);
            if (!AlgorithmCatalog.IsComposition(settings.Algorithm.Name))
            {
                Console.Error.WriteLine("compose needs a composition search --algorithm; run 'algorithms' to list them.");
                return null;
            }
            return settings;
        }
    }
}
