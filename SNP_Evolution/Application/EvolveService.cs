using System;
using SnpEvolution.Model;
using SnpEvolution.Search;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Benchmarking;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Search.Genome;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Parts;

namespace SnpEvolution.Application
{
    // The network an evolution run starts from.
    public enum RunStart
    {
        Scratch,
        NaturalNumbers,
        EvenNumbers,
    }

    // Folder is where the files go, a new folder under runs/ unless given.
    public sealed record EvolveRequest(Settings Settings, Random Random, string FileStem, RunStart Start = RunStart.Scratch, string? Folder = null);

    // Run is null, with Error saying why, when the run could not start.
    public sealed record EvolveResult(IGeneticAlgorithm? Run, bool Solved, string? Error = null)
    {
        // How the run went, in a few words.
        public string Outcome
        {
            get
            {
                string fitness = Run?.Best is Individual best ? $"best fitness {best.Fitness:0.###}" : "nothing evaluated";
                return Solved ? $"solved, {fitness}"
                    : Run is IterativeEvolution iterative ? $"stage {iterative.Stage + 1}/{iterative.StageCount}, {fitness}"
                    : fitness;
            }
        }
    }

    // Evolves a network with the settings, saves the run's files and says whether it solved its task: the menu's
    // evolve and redo, and the evolve and compose commands.
    public static class EvolveService
    {
        // The reference networks are generators, so they evolve towards the target whatever task is selected.
        public static BenchmarkTask TaskFor(Settings settings, RunStart start) => start == RunStart.Scratch
            ? settings.SelectedTask
            : Catalog.TargetTask.Create(settings);

        public static string Title(RunStart start) => start switch
        {
            RunStart.NaturalNumbers => "Natural Numbers",
            RunStart.EvenNumbers => "Evens",
            _ => "randomly generated",
        };

        public static EvolveResult Run(EvolveRequest request, Action<string> log)
        {
            Settings settings = request.Settings;
            BenchmarkTask task = TaskFor(settings, request.Start);
            ModuleLibrary? parts = null;
            if (settings.Algorithm is CompositionSearch)
            {
                Loaded<ModuleLibrary> loaded = PartLibraries.Load(settings, log);
                if (loaded.Value == null)
                {
                    return new EvolveResult(null, false, loaded.Error);
                }
                parts = loaded.Value;
            }
            EvaluationBudget evaluations = settings.RunBudget();
            IGeneticAlgorithm run = EvolutionSession.Evolve(settings, task, StartingNetwork(request.Start), request.Random, log, evaluations, parts);
            RunOutput.Save(run, request.Folder ?? RunFolders.NewOutputFolder(), request.FileStem, log, evaluations.Report());
            return new EvolveResult(run, RunLayers.IsSolved(run, task.Task));
        }

        // Starting networks always use the simple rule template; the reference networks get random expressions.
        private static Func<NetworkFactory, Network> StartingNetwork(RunStart start) => start switch
        {
            RunStart.NaturalNumbers => factory => ReferenceNetworks.NaturalNumbers().WithRandomExpressions(factory.NextExpression),
            RunStart.EvenNumbers => factory => ReferenceNetworks.EvenNumbers().WithRandomExpressions(factory.NextExpression),
            _ => factory => factory.NewNetwork(),
        };
    }
}
