using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SnpEvolution.Search;
using SnpEvolution.Search.Benchmarking;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Storage;

namespace SnpEvolution.Application
{
    // Benchmarks searches on suite tasks, or picks the best search for one task by successive halving. Both race the
    // searches that start from nothing; composition search among them builds from the settings' part library.
    public static class BenchmarkService
    {
        // The benchmark settings, with the library's parts when a composition search is racing; Error when the library cannot be read.
        public static Loaded<BenchmarkPlan> Plan(Settings settings, BenchmarkSettings benchmark, IReadOnlyList<ISearch<Individual>> searches)
        {
            if (!searches.Any(search => search is CompositionSearch))
            {
                return Loaded<BenchmarkPlan>.Of(new BenchmarkPlan(searches, benchmark, null));
            }
            return PartLibraries.Load(settings).Select(library => new BenchmarkPlan(searches, benchmark with { Parts = library.Parts.Select(module => module.Part!).ToList() },
                $"Composition search builds from {library.Parts.Count} part(s) in {settings.PartLibraryFolder}{(settings.HandBuiltParts ? " and the hand-built parts" : "")}."));
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
            string folder = RunFolders.CreateOutputFolder();
            NetworkFiles.SaveText(Benchmark.FormatTable(rows), Path.Combine(folder, "benchmark.txt"));
            NetworkFiles.SaveText(Benchmark.FormatCsv(rows), Path.Combine(folder, "benchmark.csv"));
            return folder;
        }
    }

    // The searches to race and the settings they race with. PartsNote says where composition search's parts come from.
    public sealed record BenchmarkPlan(IReadOnlyList<ISearch<Individual>> Searches, BenchmarkSettings Settings, string? PartsNote);
}
