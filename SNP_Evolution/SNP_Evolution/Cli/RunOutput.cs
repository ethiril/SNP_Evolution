using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Proposals;
using SnpEvolution.Networks;
using SnpEvolution.Storage;
using static SnpEvolution.Cli.EvolutionSession;

namespace SnpEvolution.Cli
{
    // What a finished run writes to its output folder and reports to the log.
    internal static class RunOutput
    {
        // Saves the fitness history and the best network, and reports the best network and what the run spent.
        public static void Save(IGeneticAlgorithm geneticAlgorithm, string folder, string fileStem, Action<string> log, BudgetReport? evaluations = null)
        {
            Directory.CreateDirectory(folder);
            if (evaluations != null)
            {
                log(evaluations.Describe());
                NetworkFiles.SaveText(evaluations.Describe() + Environment.NewLine, Path.Combine(folder, fileStem + "-evaluations.txt"));
            }
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
            if (Modular(geneticAlgorithm) is ModularEvolution modular)
            {
                string modules = $"{modular.SideRuns} side run(s), {modular.SideGenerationsRun} generation(s) on the side in all.{Environment.NewLine}{modular.Library.Describe()}";
                log($"\nModules:\n{modules}");
                NetworkFiles.SaveText(modules, Path.Combine(folder, fileStem + "-modules.txt"));
            }
            if (Proposals(geneticAlgorithm) is PartProposals proposals)
            {
                string parts = CompositionParts.Report(geneticAlgorithm, proposals);
                log($"\nParts:\n{parts}");
                NetworkFiles.SaveText(parts, Path.Combine(folder, fileStem + "-parts.txt"));
            }
            if (geneticAlgorithm.Best is Individual best)
            {
                string graph = NetworkNotation.Format(best.Genes);
                log($"\nBest network found (fitness {best.Fitness}, {best.Description}):\n{graph}");
                NetworkFiles.Save(best.Genes, Path.Combine(folder, fileStem + ".json"));
                NetworkFiles.SaveText(graph, Path.Combine(folder, fileStem + ".txt"));
                NetworkFiles.SaveText(NetworkGraph.Svg(best.Genes), Path.Combine(folder, fileStem + ".svg"));
                NetworkFiles.SaveText(NetworkPage.Html(best.Genes, fileStem), Path.Combine(folder, fileStem + ".html"));
            }
            log($"Saved to {folder}");
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
