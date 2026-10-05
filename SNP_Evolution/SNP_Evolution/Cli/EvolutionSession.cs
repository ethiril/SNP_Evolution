using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SnpEvolution.Evolution;
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
            if (task.Task is SequenceTask)
            {
                notes.Add("Small SN P systems produce intervals that eventually repeat, so the evolved network matches the");
                notes.Add("numbers given and need not continue the pattern after them.");
            }
            return notes;
        }

        // Starting networks always use the simple rule template; the configured templates only drive mutation.
        public static IGeneticAlgorithm Evolve(Settings settings, BenchmarkTask task, Func<NetworkFactory, Network> createStartingNetwork, Random random, Action<string> log)
        {
            GenomeSpace space = settings.GenomeSpace(task.Task.InputCount) with { RuleForm = task.RuleForm };
            var startingFactory = new NetworkFactory(space, new ExpressionGenerator(ExpressionGenerator.SimpleTemplates, Settings.MaxSpikeGroupSize, random), random);
            var mutationFactory = new NetworkFactory(space, new ExpressionGenerator(settings.MutationTemplates, Settings.MaxSpikeGroupSize, random), random);
            var evaluator = new FitnessEvaluator(
                settings.Engine.Create(settings), task.Task, settings.SimulationOptions with { Timing = task.Timing }, settings.SolvedRetestCount, random);
            IGeneticAlgorithm geneticAlgorithm = settings.Algorithm.Create(new EvolutionContext(
                settings.PopulationSize, settings.MutationRate, random, () => createStartingNetwork(startingFactory), evaluator, mutationFactory, log));
            RunGenerations(settings.MaxGenerations, geneticAlgorithm, evaluator, log);
            return geneticAlgorithm;
        }

        // Saves the fitness history and the best network, and reports the best network.
        public static void Save(IGeneticAlgorithm geneticAlgorithm, string folder, string fileStem, Action<string> log)
        {
            Directory.CreateDirectory(folder);
            NetworkFiles.SaveText(FitnessCsv.Format(geneticAlgorithm.FitnessHistory), Path.Combine(folder, fileStem + ".csv"));
            if (geneticAlgorithm is MapElites)
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

        private static void RunGenerations(int maxGenerations, IGeneticAlgorithm geneticAlgorithm, FitnessEvaluator evaluator, Action<string> log)
        {
            for (int generation = 0; generation < maxGenerations; generation++)
            {
                log($"Running Generation {generation}");
                geneticAlgorithm.NextGeneration();
                if (geneticAlgorithm.Best is not Individual best)
                {
                    continue;
                }
                log(NetworkNotation.Format(best.Genes).TrimEnd());
                log(best.Description);
                log($"Current best fitness: {best.Fitness}");
                if (!FitnessEvaluator.IsSolvingFitness(best.Fitness))
                {
                    continue;
                }
                log("Testing the best fitness for repeated success.");
                if (evaluator.IsReliablySolved(best.Genes))
                {
                    log($"Fitness over {FitnessEvaluator.SolvedThreshold}, stopping . . .");
                    return;
                }
            }
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
