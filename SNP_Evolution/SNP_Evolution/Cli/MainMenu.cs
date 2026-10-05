using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SnpEvolution.Evolution;
using SnpEvolution.Networks;
using SnpEvolution.Storage;

namespace SnpEvolution.Cli
{
    internal sealed class MainMenu
    {
        private static readonly string[] Options =
        {
            "1. Change the configuration",
            "2. Evolve a natural numbers network",
            "3. Evolve an even numbers network",
            "4. Run natural numbers network",
            "5. Run evens network",
            "6. Evolve experimental network",
            "7. Import Network from file",
            "8. Exit",
        };

        private readonly Random random = new Random();
        private Settings settings = new Settings();

        public void Run()
        {
            while (true)
            {
                switch (ConsoleUi.Choose(settings, "", Options))
                {
                    case 0:
                        settings = SettingsMenu.Edit(settings);
                        break;
                    case 1:
                        Evolve("NatNumsNet", "Natural Numbers", expressions => ReferenceNetworks.NaturalNumbers().WithRandomExpressions(expressions.Next));
                        break;
                    case 2:
                        Evolve("EvenNumsNet", "Evens", expressions => ReferenceNetworks.EvenNumbers().WithRandomExpressions(expressions.Next));
                        break;
                    case 3:
                        RunReference("Natural Numbers", ReferenceNetworks.NaturalNumbers());
                        break;
                    case 4:
                        RunReference("Even Numbers", ReferenceNetworks.EvenNumbers());
                        break;
                    case 5:
                        EvolveExperimental();
                        break;
                    case 6:
                        ImportNetwork();
                        break;
                    case 7:
                        if (ConsoleUi.Choose(settings, "Are you sure you wish to quit?", new[] { "YES", "NO" }) == 0)
                        {
                            return;
                        }
                        break;
                }
            }
        }

        private void EvolveExperimental()
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine("!!! These features are experimental and will not provide meaningful results, press enter to continue or any other key to go back !!!");
            Console.ResetColor();
            if (Console.ReadKey(true).Key == ConsoleKey.Enter)
            {
                Evolve("ExpNet", "Experimental", expressions => RandomTopology.Create(expressions, Settings.MaxSpikeGroupSize, random));
            }
        }

        // Starting networks always use the simple rule template; the configured templates only drive mutation.
        private void Evolve(string fileStem, string title, Func<ExpressionGenerator, Network> createStartingNetwork)
        {
            string folder = Path.Combine(Directory.GetCurrentDirectory(), (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond).ToString());
            Console.Clear();
            Console.WriteLine("The files will be saved to: {0}", folder);
            ConsoleUi.WaitForEnter("Press the enter key to carry out this test.");
            Console.WriteLine("---------- Evolving a network based on the {0} Spiking Neural P System ----------", title);

            var startingExpressions = new ExpressionGenerator(ExpressionGenerator.SimpleTemplates, Settings.MaxSpikeGroupSize, random);
            var mutationExpressions = new ExpressionGenerator(settings.MutationTemplates, Settings.MaxSpikeGroupSize, random);
            var evaluator = new FitnessEvaluator(
                settings.Engine.Create(settings), settings.FitnessFunction.Create(settings), settings.SimulationOptions, settings.SolvedRetestCount, random);
            IGeneticAlgorithm geneticAlgorithm = settings.Algorithm.Create(
                new EvolutionRun(settings, random, () => createStartingNetwork(startingExpressions), mutationExpressions.Next, evaluator));

            RunGenerations(geneticAlgorithm, evaluator);

            Directory.CreateDirectory(folder);
            NetworkFiles.SaveText(FitnessCsv.Format(geneticAlgorithm.FitnessHistory), Path.Combine(folder, fileStem + ".csv"));
            if (geneticAlgorithm.Best is Individual best)
            {
                string graph = NetworkNotation.Format(best.Genes);
                Console.WriteLine("\nBest network found (fitness {0}):\n{1}", best.Fitness, graph);
                NetworkFiles.Save(best.Genes, Path.Combine(folder, fileStem + ".json"));
                NetworkFiles.SaveText(graph, Path.Combine(folder, fileStem + ".txt"));
            }
            ConsoleUi.WaitForEnter("Press enter to return to the menu.");
        }

        private void RunGenerations(IGeneticAlgorithm geneticAlgorithm, FitnessEvaluator evaluator)
        {
            for (int generation = 0; generation < settings.MaxGenerations; generation++)
            {
                Console.WriteLine("Running Generation {0}", generation);
                geneticAlgorithm.NextGeneration();
                if (geneticAlgorithm.Best is not Individual best)
                {
                    continue;
                }
                Console.Write(NetworkNotation.Format(best.Genes));
                Console.WriteLine(string.Join("\t", best.Outputs.Distinct()));
                Console.WriteLine("Current best fitness: {0}", best.Fitness);
                if (!FitnessEvaluator.IsSolvingFitness(best.Fitness))
                {
                    continue;
                }
                Console.WriteLine("Testing the best fitness for repeated success.");
                if (evaluator.IsReliablySolved(best.Genes))
                {
                    Console.WriteLine("Fitness over {0}, stopping . . .", FitnessEvaluator.SolvedThreshold);
                    return;
                }
            }
        }

        private void RunReference(string title, Network network)
        {
            Console.Clear();
            Console.WriteLine("---------- Running a standard test to get an output from a {0} Network ----------", title);
            RunAndReport(network);
        }

        private void ImportNetwork()
        {
            Network? imported = null;
            ConsoleUi.PromptUntilAccepted("Please enter a filename (WITH the extension) to import from", "Could not load a network from that file.",
                path => (imported = NetworkFiles.Load(path)) != null);
            if (imported == null)
            {
                return;
            }
            Console.WriteLine("\n");
            Console.Write(NetworkNotation.Format(imported));
            RunAndReport(imported);
        }

        private void RunAndReport(Network network)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            IReadOnlyList<int> outputs = settings.Engine.Create(settings).CollectOutputs(new[] { network }, settings.SimulationOptions, random)[0];
            stopwatch.Stop();
            Console.WriteLine("Final output set: ");
            Console.WriteLine(string.Join("\t", outputs));
            ConsoleUi.WaitForEnter($"Time elapsed: {stopwatch.Elapsed}. Press enter to return to the menu.");
        }
    }
}
