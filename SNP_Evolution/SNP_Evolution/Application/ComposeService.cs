using System;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Search;

namespace SnpEvolution.Application
{
    // Composition search for one suite task: a contract it solves is promoted to a part and the library saved.
    internal static class ComposeService
    {
        public const string FileStem = "ComposedNet";

        // The settings compose starts from, before any options: the task's rule form and timing, composition search with
        // tournament selection and lexicase parents, and a budget large enough for the arithmetic contracts.
        public static Settings SettingsFor(CatalogEntry<Settings, BenchmarkTask> task)
        {
            BenchmarkTask suiteTask = task.Create(new Settings());
            return new Settings
            {
                Task = task,
                RuleForm = suiteTask.RuleForm,
                OutputTiming = suiteTask.Timing,
                Algorithm = SearchCatalog.CompositionTournament,
                MaxEvaluations = 30_000,
                MaxGenerations = 5_000,
                Lexicase = true,
            };
        }

        // Why these settings cannot compose, or null when they can.
        public static string? Problem(Settings settings) =>
            settings.Algorithm is CompositionSearch ? null : "compose needs a composition search --algorithm; run 'algorithms' to list them.";

        public static EvolveResult Run(Settings settings, Random random, Action<string> log) =>
            EvolveService.Run(new EvolveRequest(settings, random, FileStem), log);
    }
}
