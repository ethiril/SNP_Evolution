using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Search;
using SnpEvolution.Storage;

namespace SnpEvolution.Application
{
    // Benchmarks searches on suite tasks, or picks the best search for one task by successive halving. Both race the
    // searches that start from nothing; composition search among them builds from the settings' part library.
    internal static class BenchmarkService
    {
        // The benchmark settings, with the library's parts when a composition search is racing; Error when the library cannot be read.
        public static Loaded<BenchmarkPlan> Plan(Settings settings, BenchmarkSettings benchmark, IReadOnlyList<ISearch<Individual>> searches)
        {
            if (!searches.Any(search => search is CompositionSearch))
            {
                return Loaded<BenchmarkPlan>.Of(new BenchmarkPlan(searches, benchmark, null));
            }
            Loaded<ModuleLibrary> library = PartLibraries.Load(settings);
            if (library.Value == null)
            {
                return Loaded<BenchmarkPlan>.Failed(library.Error!);
            }
            string from = $"Composition search builds from {library.Value.Parts.Count} part(s) in {settings.PartLibraryFolder}{(settings.HandBuiltParts ? " and the hand-built parts" : "")}.";
            return Loaded<BenchmarkPlan>.Of(new BenchmarkPlan(searches, benchmark with { Parts = library.Value.Parts.Select(module => module.Part!).ToList() }, from));
        }

        public static IReadOnlyList<BenchmarkRow> Run(BenchmarkPlan plan, IReadOnlyList<BenchmarkTask> tasks, Action<string> log) =>
            Benchmark.Run(plan.Searches, tasks, plan.Settings, log);

        // Starts every search at an eighth of the benchmark budget.
        public static SelectionResult<ISearch<Individual>> Select(BenchmarkPlan plan, BenchmarkTask task, Action<string> log) =>
            AlgorithmSelector.Select(plan.Searches, task, plan.Settings, InitialBudget(plan.Settings), log);

        public static long InitialBudget(BenchmarkSettings benchmark) => Math.Max(1, benchmark.EvaluationBudget / 8);

        // benchmark.txt and benchmark.csv in a new run folder, which is returned.
        public static string Save(IReadOnlyList<BenchmarkRow> rows)
        {
            string folder = RunFolders.NewOutputFolder();
            Directory.CreateDirectory(folder);
            NetworkFiles.SaveText(Benchmark.FormatTable(rows), Path.Combine(folder, "benchmark.txt"));
            NetworkFiles.SaveText(Benchmark.FormatCsv(rows), Path.Combine(folder, "benchmark.csv"));
            return folder;
        }
    }

    // The searches to race and the settings they race with. PartsNote says where composition search's parts come from.
    internal sealed record BenchmarkPlan(IReadOnlyList<ISearch<Individual>> Searches, BenchmarkSettings Settings, string? PartsNote);
}
