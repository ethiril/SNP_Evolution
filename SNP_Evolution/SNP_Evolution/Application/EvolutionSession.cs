using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Genome;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Proposals;
using SnpEvolution.Evolution.Search;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;

namespace SnpEvolution.Application
{
    // Builds one evolution run from the settings, with the stages, modules, proposals and stagnation recovery they ask
    // for, and runs it to the end; the run services call it.
    internal static class EvolutionSession
    {
        // Whether this run evolves the target a few values at a time, and if so for what.
        public static bool IsIterative(Settings settings, BenchmarkTask task, [NotNullWhen(true)] out IPrefixTask? prefixTask)
        {
            prefixTask = settings.IterativeEvolution && task.Task is IPrefixTask candidate && settings.CurriculumFor(candidate).Lengths(candidate.Length).Count > 1
                ? candidate
                : null;
            return prefixTask != null;
        }

        // Starting networks always use the simple rule template; the configured templates only drive mutation.
        // Every evaluation is charged to the budget, side runs, incubation, retests and proposed parts included.
        public static IGeneticAlgorithm Evolve(Settings settings, BenchmarkTask task, Func<NetworkFactory, Network> createStartingNetwork, Random random, Action<string> log,
            EvaluationBudget evaluations, ModuleLibrary? parts = null)
        {
            bool composition = settings.Algorithm is CompositionSearch;
            parts = composition ? parts ?? LoadParts(settings, log) : null;
            int partsAtStart = parts?.Parts.Count ?? 0;
            evaluations.AddUpFront(parts?.PartEvaluations ?? 0);
            GenomeSpace space = settings.GenomeSpace(task.Task.InputCount) with { RuleForm = task.RuleForm };
            NetworkFactory StartingFactory(GenomeSpace bounds) => new NetworkFactory(bounds, new ExpressionGenerator(ExpressionGenerator.SimpleTemplates, Settings.MaxSpikeGroupSize, random), random);
            NetworkFactory MutationFactory(GenomeSpace bounds) => new NetworkFactory(bounds, new ExpressionGenerator(settings.MutationTemplates, Settings.MaxSpikeGroupSize, random), random);
            NetworkFactory startingFactory = StartingFactory(space);
            NetworkFactory mutationFactory = MutationFactory(space);
            // Harvested modules are not verified parts, so composition search leaves the modular loop out.
            ModuleLibrary? library = settings.Modules && !composition ? NewLibrary(settings, log) : null;
            FitnessEvaluator CreateEvaluator(ITask stageTask, EvaluationSource source = EvaluationSource.Main) => new FitnessEvaluator(
                settings.Engine.Create(settings), stageTask, settings.SimulationOptions with { Timing = task.Timing }, settings.SolvedRetestCount, random, evaluations, source);
            EvolutionContext Context(IPopulationEvaluator evaluator, MutationPressure? pressure, ModuleTracker? tracker, Func<Network>? starting = null, NetworkFactory? mutation = null) =>
                new EvolutionContext(settings.PopulationSize, settings.MutationRate, random, starting ?? (() => createStartingNetwork(startingFactory)), evaluator,
                    mutation ?? mutationFactory, log, pressure, settings.Lexicase, library != null ? new ModuleSupport(library, settings.FreezeModules, tracker) : null,
                    parts, settings.Composition);
            // A side run for a part starts from random networks no bigger than a module, with the part's inputs,
            // whatever the main run started from; one that incubates networks for the run's task starts from them.
            IGeneticAlgorithm SideRun(ITask sideTask, IReadOnlyList<Network>? seeds)
            {
                if (seeds != null)
                {
                    return settings.Algorithm.Create(Context(CreateEvaluator(sideTask, EvaluationSource.Incubation), null, null, () => seeds[random.Next(seeds.Count)]));
                }
                GenomeSpace part = space with
                {
                    InputCount = sideTask.InputCount,
                    MaxNeurons = Math.Min(space.MaxNeurons, sideTask.InputCount + ModuleLibrary.MaxModuleNeurons),
                };
                NetworkFactory partStarting = StartingFactory(part);
                return settings.Algorithm.Create(Context(CreateEvaluator(sideTask, EvaluationSource.SideRun), null, null, partStarting.NewNetwork, MutationFactory(part)));
            }
            IGeneticAlgorithm CreateAlgorithm(IPopulationEvaluator evaluator)
            {
                var pressure = new MutationPressure();
                ModuleTracker? tracker = library != null ? new ModuleTracker(library) : null;
                EvolutionContext context = Context(tracker?.Watch(evaluator) ?? evaluator, pressure, tracker);
                IGeneticAlgorithm geneticAlgorithm = settings.Algorithm.Create(context);
                if (library != null)
                {
                    // Side runs share the library but not the tracker, which follows the main task.
                    Func<ITask> currentTask = evaluator is ITaskEvaluator taskEvaluator ? () => taskEvaluator.Task : () => task.Task;
                    geneticAlgorithm = new ModularEvolution(geneticAlgorithm, library, tracker, currentTask, SideRun,
                        settings.ModulePolicy, settings.PopulationSize, space.MaxNeurons, random, log);
                }
                if (composition)
                {
                    Func<ITask> scoredOn = evaluator is ITaskEvaluator scoring ? () => scoring.Task : () => task.Task;
                    geneticAlgorithm = new PartProposals(geneticAlgorithm, CompositionSpace.For(context), scoredOn, ProposedPart,
                        settings.ProposalPolicy, evaluations, settings.PopulationSize, log);
                }
                if (!settings.StagnationRecovery)
                {
                    return geneticAlgorithm;
                }
                return settings.Algorithm.Recovering(geneticAlgorithm, context, settings.StagnationPolicy, pressure, startingFactory.NewNetwork,
                    WeightedMutation.Structural(1, mutationFactory, modules: library != null ? new ModuleSupport(library, settings.FreezeModules) : null));
            }
            PartOutcome ProposedPart(Contract contract)
            {
                return PartSearch.Evolve(contract, random.Next(), PartsService.SearchSettings(settings.ProposalBudget, () => new ExhaustiveCpuEngine()) with { HardwareProfile = settings.HardwareProfile },
                    evaluations.Phase(source: EvaluationSource.Proposals), log);
            }
            if (IsIterative(settings, task, out IPrefixTask? prefixTask))
            {
                var iterative = new IterativeEvolution(prefixTask, settings.CurriculumFor(prefixTask).Lengths(prefixTask.Length), stageTask => CreateEvaluator(stageTask), CreateAlgorithm, log);
                RunGenerations(settings, evaluations, iterative, _ => iterative.IsComplete, log);
                PartLibraries.SaveIfGrown(settings, parts, partsAtStart, log);
                return iterative;
            }
            FitnessEvaluator evaluator = CreateEvaluator(task.Task);
            IGeneticAlgorithm run = CreateAlgorithm(evaluator);
            RunGenerations(settings, evaluations, run, best =>
            {
                if (Solved.Solves(best.Fitness))
                {
                    log("Testing the best fitness for repeated success.");
                }
                return SolveCheck.Confirms(best, evaluator);
            }, log);
            if (parts != null && task.Task is IContractTask contractTask && RunLayers.IsSolved(run, contractTask) && run.Best is Individual solved)
            {
                Promotion.PromoteSolved(solved.Genes, ContractTask.Of(contractTask), parts, new PartOrigin(0, $"composition search for {contractTask.Contract.Name}", evaluations.Networks), evaluations, log);
            }
            PartLibraries.SaveIfGrown(settings, parts, partsAtStart, log);
            return run;
        }

        // The run services load the library first to report a broken one; a caller that does not gets the reason thrown.
        private static ModuleLibrary LoadParts(Settings settings, Action<string> log)
        {
            Loaded<ModuleLibrary> loaded = PartLibraries.Load(settings, log);
            return loaded.Value ?? throw new InvalidDataException(loaded.Error);
        }

        // An empty library, or one started with the saved networks given; a file that cannot be read is skipped.
        private static ModuleLibrary NewLibrary(Settings settings, Action<string> log)
        {
            var library = new ModuleLibrary(log: log);
            foreach (string file in settings.ModuleFiles)
            {
                if (NetworkFiles.Load(file) is Network network)
                {
                    library.Add(ModuleCuts.Whole(network), $"the file {Path.GetFileName(file)}");
                }
            }
            return library;
        }

        private static void RunGenerations(Settings settings, EvaluationBudget evaluations, IGeneticAlgorithm geneticAlgorithm, Func<Individual, bool> isSolved, Action<string> log)
        {
            (SearchStop stop, int generations) = GenerationLoop.Run(settings.MaxGenerations, () => evaluations.IsSpent, default, generation =>
            {
                string stage = geneticAlgorithm is IterativeEvolution iterative ? $" (stage {iterative.Stage + 1}/{iterative.StageCount})" : "";
                log($"Running Generation {generation}{stage}");
                geneticAlgorithm.NextGeneration();
                if (geneticAlgorithm.Best is not Individual best)
                {
                    return false;
                }
                log(NetworkNotation.Format(best.Genes).TrimEnd());
                log(best.Description);
                log($"Current best fitness: {best.Fitness}");
                return isSolved(best);
            });
            if (stop == SearchStop.Solved)
            {
                log($"Fitness over {Solved.Sampled}, stopping . . .");
            }
            else if (stop == SearchStop.BudgetSpent && generations < settings.MaxGenerations)
            {
                log($"The budget of {settings.MaxEvaluations} evaluations is spent, stopping . . .");
            }
        }
    }
}
