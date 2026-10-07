using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Proposals;
using SnpEvolution.Networks;
using SnpEvolution.Storage;
using static SnpEvolution.Application.RunLayers;

namespace SnpEvolution.Application
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
                string parts = PartsReport(geneticAlgorithm, proposals);
                log($"\nParts:\n{parts}");
                NetworkFiles.SaveText(parts, Path.Combine(folder, fileStem + "-parts.txt"));
            }
            if (geneticAlgorithm.Best is Individual best)
            {
                string graph = NetworkNotation.Format(best.Genes);
                log($"\nBest network found (fitness {best.Fitness}, {best.Description}):\n{graph}");
                SaveNetwork(best.Genes, folder, fileStem);
            }
            log($"Saved to {folder}");
        }

        // A network as JSON, as a text table, and drawn as SVG and as a page.
        public static void SaveNetwork(Network network, string folder, string stem)
        {
            NetworkFiles.Save(network, Path.Combine(folder, stem + ".json"));
            NetworkFiles.SaveText(NetworkNotation.Format(network), Path.Combine(folder, stem + ".txt"));
            NetworkFiles.SaveText(NetworkGraph.Svg(network), Path.Combine(folder, stem + ".svg"));
            NetworkFiles.SaveText(NetworkPage.Html(network, stem), Path.Combine(folder, stem + ".html"));
        }

        private static string PartsReport(IGeneticAlgorithm geneticAlgorithm, PartProposals proposals) =>
            $"{proposals.Describe()}{Environment.NewLine}{Environment.NewLine}{PartReuse.Describe(geneticAlgorithm.Best?.Genes, geneticAlgorithm.Population.Select(individual => individual.Genes), proposals.Library)}"
            + (geneticAlgorithm.Best is Individual composed ? Environment.NewLine + UsesPromoted(composed.Genes, proposals.Library) : "");

        // A best network built on a promoted part is the sign that the library compounds.
        private static string UsesPromoted(Network network, ModuleLibrary library)
        {
            List<string> promoted = PartReuse.Count(network, library)
                .Where(count => count.Direct > 0 && library.PartFor(count.Contract)?.Part!.IsComposite == true)
                .Select(count => $"{count.Contract} x{count.Direct}")
                .ToList();
            return promoted.Count > 0 ? $"The best network reuses promoted part(s): {string.Join(", ", promoted)}." : "The best network uses no promoted part.";
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
