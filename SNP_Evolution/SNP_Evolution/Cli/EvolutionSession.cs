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

namespace SnpEvolution.Cli
{
    // One evolution run with the current settings, shared by the menu and the evolve command.
    internal static class EvolutionSession
    {
        // Beside parts/, and ignored by git, so runs never land in the source tree.
        public const string RunsFolder = "runs";

        public static string NewOutputFolder() =>
            Path.Combine(Settings.WorkingRoot(), RunsFolder, (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond).ToString());

        // What the user should know before evolving for this task: limits of the target and changed settings.
        public static IReadOnlyList<string> Notes(Settings settings, BenchmarkTask task)
        {
            var notes = new List<string>();
            if (task.Task.StepsNeeded > settings.MaxSteps)
            {
                notes.Add($"Runs last {task.Task.StepsNeeded} steps instead of {settings.MaxSteps}, so the whole target fits.");
            }
            if (task.Task.Cases.Any(@case => @case.Readout == Readout.SpikeTrain))
            {
                notes.Add("Only a network that gives this output on every run solves the task.");
            }
            if (IsIterative(settings, task, out IPrefixTask? prefixTask))
            {
                IReadOnlyList<int> lengths = settings.CurriculumFor(prefixTask).Lengths(prefixTask.Length);
                notes.Add($"Evolving iteratively in {lengths.Count} stages, from the first {lengths[0]} values to all {prefixTask.Length}.");
            }
            if (settings.Modules)
            {
                notes.Add(settings.ModuleFiles.Count > 0
                    ? $"Building from modules, starting with {settings.ModuleFiles.Count} saved network(s) and adding any the run finds."
                    : "Building from modules the run finds: changes that pay off, solved stages and side runs on what is missing.");
                if (settings.ModuleIncubation > 0)
                {
                    notes.Add($"Networks given a new module evolve apart for up to {settings.ModuleIncubation} generations before joining the run" +
                        (settings.TriggeredModules ? "; every other side run builds a part that starts on a trigger from the host." : "."));
                }
            }
            if (settings.Algorithm is CompositionSearch)
            {
                notes.Add($"Composing networks from the parts in {settings.PartLibraryFolder}{(settings.HandBuiltParts ? " and the hand-built parts" : "")}, with up to {(settings.Composition.MaxGlue > 0 ? settings.Composition.MaxGlue : settings.MaxNeurons)} glue neuron(s) and {settings.Composition.MaxParts} part copies"
                    + (settings.Modules ? "; the modular loop is left out, since harvested modules are not parts." : "."));
                if (settings.ProposeParts)
                {
                    notes.Add($"When the run stalls it proposes the parts it lacks and evolves each for up to {settings.ProposalBudget} evaluations.");
                }
                if (task.Task is IContractTask)
                {
                    notes.Add("A composition that solves the contract is promoted to a part and saved to the library.");
                }
            }
            if (settings.MaxEvaluations > 0)
            {
                notes.Add($"The run stops after {settings.MaxEvaluations} evaluations, counting side runs, incubation and retests.");
            }
            if (settings.Lexicase)
            {
                notes.Add("Parents are picked by lexicase selection, so networks right about different parts of the target all breed.");
            }
            if (settings.StagnationRecovery)
            {
                notes.Add($"After {settings.StagnationPatience} generations without improvement, mutation steps up and newcomers join.");
            }
            if (task.Task is ISequenceTask)
            {
                notes.Add("Small SN P systems produce intervals that eventually repeat, so the evolved network matches the");
                notes.Add("numbers given and need not continue the pattern after them.");
            }
            return notes;
        }

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
            EvaluationBudget evaluations)
        {
            bool composition = settings.Algorithm is CompositionSearch;
            ModuleLibrary? parts = composition ? CompositionParts.Load(settings, log) : null;
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
                return PartSearch.Evolve(contract, random.Next(), PartsSession.SearchSettings(settings.ProposalBudget, () => new ExhaustiveCpuEngine()) with { HardwareProfile = settings.HardwareProfile },
                    evaluations.Phase(source: EvaluationSource.Proposals), log);
            }
            if (IsIterative(settings, task, out IPrefixTask? prefixTask))
            {
                var iterative = new IterativeEvolution(prefixTask, settings.CurriculumFor(prefixTask).Lengths(prefixTask.Length), stageTask => CreateEvaluator(stageTask), CreateAlgorithm, log);
                RunGenerations(settings, evaluations, iterative, _ => iterative.IsComplete, log);
                CompositionParts.SaveIfGrown(settings, parts, partsAtStart, log);
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
            if (parts != null && task.Task is IContractTask contractTask && IsSolved(run, contractTask) && run.Best is Individual solved)
            {
                Promotion.PromoteSolved(solved.Genes, ContractTask.Of(contractTask), parts, new PartOrigin(0, $"composition search for {contractTask.Contract.Name}", evaluations.Networks), evaluations, log);
            }
            CompositionParts.SaveIfGrown(settings, parts, partsAtStart, log);
            return run;
        }

        // True when the whole target was matched, rather than only an early stage of it.
        public static bool IsSolved(IGeneticAlgorithm geneticAlgorithm, ITask? task = null) =>
            geneticAlgorithm is IterativeEvolution iterative
                ? iterative.IsComplete
                : geneticAlgorithm.Best is Individual best && Solved.Solves(best.Fitness, task);

        // The algorithm doing the work, without the stages, modules and stagnation recovery around it.
        public static IGeneticAlgorithm Unwrap(IGeneticAlgorithm geneticAlgorithm) => geneticAlgorithm switch
        {
            IterativeEvolution iterative => Unwrap(iterative.Algorithm),
            StagnationRecovery recovery => Unwrap(recovery.Inner),
            ModularEvolution modular => Unwrap(modular.Inner),
            PartProposals proposals => Unwrap(proposals.Inner),
            _ => geneticAlgorithm,
        };

        public static PartProposals? Proposals(IGeneticAlgorithm geneticAlgorithm) => geneticAlgorithm switch
        {
            PartProposals proposals => proposals,
            IterativeEvolution iterative => Proposals(iterative.Algorithm),
            StagnationRecovery recovery => Proposals(recovery.Inner),
            _ => null,
        };

        // The modular loop somewhere in the run, if it has one.
        public static ModularEvolution? Modular(IGeneticAlgorithm geneticAlgorithm) => geneticAlgorithm switch
        {
            ModularEvolution modular => modular,
            IterativeEvolution iterative => Modular(iterative.Algorithm),
            StagnationRecovery recovery => Modular(recovery.Inner),
            _ => null,
        };

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
