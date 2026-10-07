using System;
using SnpEvolution.Search;

namespace SnpEvolution.Application
{
    // Composition search for one suite task: a contract it solves is promoted to a part and the library saved.
    public static class ComposeService
    {
        public const string FileStem = "ComposedNet";

        // The settings compose starts from, before its task and options: composition search with tournament selection and
        // lexicase parents, and a budget large enough for the arithmetic contracts.
        public static Settings Starting(Settings settings)
        {
            Settings starting = settings.Copy();
            starting.Algorithm = SearchCatalog.CompositionTournament;
            starting.MaxEvaluations = 30_000;
            starting.MaxGenerations = 5_000;
            starting.Lexicase = true;
            return starting;
        }

        // Why these settings cannot compose, or null when they can.
        public static string? Problem(Settings settings) =>
            settings.Algorithm is CompositionSearch ? null : "compose needs a composition search --algorithm; run 'algorithms' to list them.";

        public static EvolveResult Run(Settings settings, Random random, Action<string> log) =>
            EvolveService.Run(new EvolveRequest(settings, random, FileStem), log);
    }
}
