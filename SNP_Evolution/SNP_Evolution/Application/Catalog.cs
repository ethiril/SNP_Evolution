using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Search;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Simulation;
using SnpEvolution.Simulation.Metal;

namespace SnpEvolution.Application
{
    internal sealed record CatalogEntry<TContext, T>(string Name, Func<TContext, T> Create);

    // The swappable parts offered in the settings menu. A new engine, fitness function or task only needs to implement
    // its interface and be listed here, and a new search to be registered in SearchCatalog; the first entry of each list
    // is the default.
    internal static class Catalog
    {
        // The default: the GPU where there is one, which itself hands batches too small for it to the CPU.
        public static readonly CatalogEntry<Settings, ISimulationEngine> AutoEngine = MetalEngine.IsAvailable
            ? new CatalogEntry<Settings, ISimulationEngine>("Auto: fastest available (GPU here, CPU for small batches)", _ => MetalEngine.OrCpu())
            : new CatalogEntry<Settings, ISimulationEngine>("Auto: fastest available (CPU, all cores here)", _ => new ParallelCpuEngine());

        public static readonly IReadOnlyList<CatalogEntry<Settings, ISimulationEngine>> Engines = new[]
        {
            AutoEngine,
            new CatalogEntry<Settings, ISimulationEngine>("CPU, all cores", _ => new ParallelCpuEngine()),
            new CatalogEntry<Settings, ISimulationEngine>("CPU, single thread", _ => new SequentialCpuEngine()),
            new CatalogEntry<Settings, ISimulationEngine>("Exhaustive (exact outputs), all cores", _ => new ExhaustiveCpuEngine()),
        }
        .Concat(MetalEngine.IsAvailable
            ? new[] { new CatalogEntry<Settings, ISimulationEngine>("GPU (Metal), for large networks", _ => MetalEngine.OrCpu()) }
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

        // The genetic algorithms a run can evolve with, from the search catalog.
        public static IReadOnlyList<EvolutionSearch> Algorithms => SearchCatalog.Evolution;

        public static EvolutionSearch StructuralDefault => SearchCatalog.StructuralDefault;

        // The items whose name contains the text, ignoring case, or only those named exactly by it when there are any;
        // every item when there is no text. Commands pick tasks, searches and contracts by name this way.
        public static List<T> Matching<T>(IEnumerable<T> items, Func<T, string> name, string? text)
        {
            List<T> containing = items.Where(item => text == null || name(item).Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
            List<T> exact = containing.Where(item => string.Equals(name(item), text, StringComparison.OrdinalIgnoreCase)).ToList();
            return exact.Count > 0 ? exact : containing;
        }
    }
}
