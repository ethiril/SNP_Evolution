using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;

namespace SnpEvolution.Cli
{
    // One evolution run with the current settings, shared by the menu and the evolve command.
    internal static class EvolutionSession
    {
        public static string NewOutputFolder() =>
            Path.Combine(Directory.GetCurrentDirectory(), (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond).ToString());

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
                if (settings.Engine.Name.StartsWith("Exhaustive"))
                {
                    notes.Add("Spike trains are always sampled, so the exhaustive engine samples this task too.");
                }
                notes.Add("Only a network that gives this output on every run solves the task.");
            }
            if (IsIterative(settings, task, out IPrefixTask? prefixTask))
            {
                IReadOnlyList<int> lengths = settings.CurriculumFor(prefixTask).Lengths(prefixTask.Length);
                notes.Add($"Evolving iteratively in {lengths.Count} stages, from the first {lengths[0]} values to all {prefixTask.Length}.");
            }
            if (settings.StagnationRecovery)
            {
                notes.Add($"After {settings.StagnationPatience} generations without improvement, mutation steps up and newcomers join.");
            }
            if (task.Task is SequenceTask)
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
        public static IGeneticAlgorithm Evolve(Settings settings, BenchmarkTask task, Func<NetworkFactory, Network> createStartingNetwork, Random random, Action<string> log)
        {
            GenomeSpace space = settings.GenomeSpace(task.Task.InputCount) with { RuleForm = task.RuleForm };
            var startingFactory = new NetworkFactory(space, new ExpressionGenerator(ExpressionGenerator.SimpleTemplates, Settings.MaxSpikeGroupSize, random), random);
            var mutationFactory = new NetworkFactory(space, new ExpressionGenerator(settings.MutationTemplates, Settings.MaxSpikeGroupSize, random), random);
            FitnessEvaluator CreateEvaluator(ITask stageTask) => new FitnessEvaluator(
                settings.Engine.Create(settings), stageTask, settings.SimulationOptions with { Timing = task.Timing }, settings.SolvedRetestCount, random);
            IGeneticAlgorithm CreateAlgorithm(IPopulationEvaluator evaluator)
            {
                var pressure = new MutationPressure();
                IGeneticAlgorithm geneticAlgorithm = settings.Algorithm.Create(new EvolutionContext(
                    settings.PopulationSize, settings.MutationRate, random, () => createStartingNetwork(startingFactory), evaluator, mutationFactory, log, pressure));
                return settings.StagnationRecovery
                    ? new StagnationRecovery(geneticAlgorithm, settings.StagnationPolicy, settings.PopulationSize, pressure,
                        startingFactory.NewNetwork, WeightedMutation.Structural(1, mutationFactory), random, log)
                    : geneticAlgorithm;
            }
            if (IsIterative(settings, task, out IPrefixTask? prefixTask))
            {
                var iterative = new IterativeEvolution(prefixTask, settings.CurriculumFor(prefixTask).Lengths(prefixTask.Length), CreateEvaluator, CreateAlgorithm, log);
                RunGenerations(settings.MaxGenerations, iterative, _ => iterative.IsComplete, log);
                return iterative;
            }
            FitnessEvaluator evaluator = CreateEvaluator(task.Task);
            IGeneticAlgorithm run = CreateAlgorithm(evaluator);
            RunGenerations(settings.MaxGenerations, run, best =>
            {
                if (!FitnessEvaluator.IsSolvingFitness(best.Fitness))
                {
                    return false;
                }
                log("Testing the best fitness for repeated success.");
                return evaluator.IsReliablySolved(best.Genes);
            }, log);
            return run;
        }

        // True when the whole target was matched, rather than only an early stage of it.
        public static bool IsSolved(IGeneticAlgorithm geneticAlgorithm) =>
            geneticAlgorithm is IterativeEvolution iterative
                ? iterative.IsComplete
                : geneticAlgorithm.Best is Individual best && FitnessEvaluator.IsSolvingFitness(best.Fitness);

        // The algorithm doing the work, without the stages and stagnation recovery around it.
        public static IGeneticAlgorithm Unwrap(IGeneticAlgorithm geneticAlgorithm) => geneticAlgorithm switch
        {
            IterativeEvolution iterative => Unwrap(iterative.Algorithm),
            StagnationRecovery recovery => Unwrap(recovery.Inner),
            _ => geneticAlgorithm,
        };

        // Saves the fitness history and the best network, and reports the best network.
        public static void Save(IGeneticAlgorithm geneticAlgorithm, string folder, string fileStem, Action<string> log)
        {
            Directory.CreateDirectory(folder);
            NetworkFiles.SaveText(FitnessCsv.Format(geneticAlgorithm.FitnessHistory), Path.Combine(folder, fileStem + ".csv"));
            if (geneticAlgorithm is IterativeEvolution iterative)
            {
                string stages = FormatStages(iterative);
                log($"\nStages:\n{stages}");
                NetworkFiles.SaveText(stages, Path.Combine(folder, fileStem + "-stages.txt"));
                if (!iterative.IsComplete)
                {
                    log($"Stopped at stage {iterative.Stage + 1}/{iterative.StageCount}, so the best network below is scored on the first {iterative.StageLength} values only.");
                }
            }
            if (Unwrap(geneticAlgorithm) is MapElites)
            {
                string front = SizeFront(geneticAlgorithm.Population);
                log($"\nThe fittest network of each size:\n{front}");
                NetworkFiles.SaveText(front, Path.Combine(folder, fileStem + "-sizes.txt"));
            }
            if (geneticAlgorithm.Best is Individual best)
            {
                string graph = NetworkNotation.Format(best.Genes);
                log($"\nBest network found (fitness {best.Fitness}, {best.Description}):\n{graph}");
                NetworkFiles.Save(best.Genes, Path.Combine(folder, fileStem + ".json"));
                NetworkFiles.SaveText(graph, Path.Combine(folder, fileStem + ".txt"));
            }
            log($"Saved to {folder}");
        }

        private static void RunGenerations(int maxGenerations, IGeneticAlgorithm geneticAlgorithm, Func<Individual, bool> isSolved, Action<string> log)
        {
            for (int generation = 0; generation < maxGenerations; generation++)
            {
                string stage = geneticAlgorithm is IterativeEvolution iterative ? $" (stage {iterative.Stage + 1}/{iterative.StageCount})" : "";
                log($"Running Generation {generation}{stage}");
                geneticAlgorithm.NextGeneration();
                if (geneticAlgorithm.Best is not Individual best)
                {
                    continue;
                }
                log(NetworkNotation.Format(best.Genes).TrimEnd());
                log(best.Description);
                log($"Current best fitness: {best.Fitness}");
                if (isSolved(best))
                {
                    log($"Fitness over {FitnessEvaluator.SolvedThreshold}, stopping . . .");
                    return;
                }
            }
        }

        private static string FormatStages(IterativeEvolution iterative)
        {
            var lines = new List<string> { "Stage  Values  Generations  Best fitness" };
            lines.AddRange(iterative.Stages.Select((stage, index) =>
                $"{index + 1,5}  {stage.Length,6}  {stage.Generations,11}  {stage.BestFitness:0.000}{(stage.Solved ? "   solved" : "")}"));
            return string.Join(Environment.NewLine, lines) + Environment.NewLine;
        }

        // Each size's best network, smallest first, skipping any that a smaller network already matches.
        private static string SizeFront(IReadOnlyList<Individual> elites)
        {
            var lines = new List<string> { "Neurons  Rules  Fitness" };
            float bestSoFar = float.MinValue;
            foreach (Individual elite in elites.OrderBy(elite => elite.Genes.Size))
            {
                if (elite.Fitness > bestSoFar)
                {
                    bestSoFar = elite.Fitness;
                    lines.Add($"{elite.Genes.Neurons.Count,7}  {elite.Genes.RuleCount,5}  {elite.Fitness:0.000}   {elite.Description}");
                }
            }
            return string.Join(Environment.NewLine, lines) + Environment.NewLine;
        }
    }
}
