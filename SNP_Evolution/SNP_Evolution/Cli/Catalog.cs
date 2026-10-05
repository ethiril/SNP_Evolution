using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Cli
{
    internal sealed record CatalogEntry<TContext, T>(string Name, Func<TContext, T> Create);

    // The swappable parts offered in the settings menu. A new engine, fitness function, task or algorithm only
    // needs to implement its interface and be listed here; the first entry of each list is the default.
    internal static class Catalog
    {
        public static readonly IReadOnlyList<CatalogEntry<Settings, ISimulationEngine>> Engines = new[]
        {
            new CatalogEntry<Settings, ISimulationEngine>("CPU, all cores", _ => new ParallelCpuEngine()),
            new CatalogEntry<Settings, ISimulationEngine>("CPU, single thread", _ => new SequentialCpuEngine()),
            new CatalogEntry<Settings, ISimulationEngine>("Exhaustive (exact outputs), all cores", _ => new ExhaustiveCpuEngine()),
        }
        .Concat(MetalEngine.IsAvailable
            ? new[] { new CatalogEntry<Settings, ISimulationEngine>("GPU (Metal), for large networks", _ => new MetalEngine()) }
            : Array.Empty<CatalogEntry<Settings, ISimulationEngine>>())
        .ToList();

        public static readonly IReadOnlyList<CatalogEntry<Settings, IFitnessFunction>> FitnessFunctions = new[]
        {
            new CatalogEntry<Settings, IFitnessFunction>("Set coverage (F1)", settings => new SetCoverageFitness(settings.Target.Values)),
            new CatalogEntry<Settings, IFitnessFunction>("Jaccard similarity", settings => new JaccardFitness(settings.Target.Values)),
        };

        // The target from the settings first, then the benchmark suite.
        public static readonly CatalogEntry<Settings, BenchmarkTask> TargetTask = new CatalogEntry<Settings, BenchmarkTask>("Match the target",
            settings => new BenchmarkTask(settings.Target.CreateTask(settings.FitnessFunction.Create(settings)), settings.RuleForm, settings.OutputTiming));

        public static readonly IReadOnlyList<CatalogEntry<Settings, BenchmarkTask>> Tasks =
            new[] { TargetTask }
            .Concat(TaskSuite.All.Select(task => new CatalogEntry<Settings, BenchmarkTask>(task.Name, _ => task)))
            .ToList();

        public static readonly IReadOnlyList<CatalogEntry<EvolutionContext, IGeneticAlgorithm>> Algorithms =
            AlgorithmCatalog.All.Select(choice => new CatalogEntry<EvolutionContext, IGeneticAlgorithm>(choice.Name, choice.Create)).ToList();

        // The default algorithm: it evolves structure as well as rules, and has done best from scratch so far.
        public static CatalogEntry<EvolutionContext, IGeneticAlgorithm> StructuralDefault => Algorithms.First(entry => entry.Name.StartsWith("MAP-Elites"));

        // Rule-only algorithms keep the starting network's structure, so they cannot build a network from scratch.
        public static bool EvolvesRulesOnly(CatalogEntry<EvolutionContext, IGeneticAlgorithm> entry) => EvolvesRulesOnly(entry.Name);

        public static bool EvolvesRulesOnly(string algorithmName) => algorithmName.Contains("rule expressions only");

        public static AlgorithmChoice ChoiceFor(CatalogEntry<EvolutionContext, IGeneticAlgorithm> entry) => new AlgorithmChoice(entry.Name, entry.Create);
    }
}
