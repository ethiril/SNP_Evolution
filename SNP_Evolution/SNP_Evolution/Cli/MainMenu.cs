using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;

namespace SnpEvolution.Cli
{
    // The top menu groups the work into evolving, running, benchmarking and settings. Every submenu stays open until
    // the user goes back, so several things can be done in a row.
    internal sealed class MainMenu
    {
        private readonly Random random = new Random();
        private Settings settings = new Settings();

        public void Run()
        {
            int selection = 0;
            while (true)
            {
                int? choice = ConsoleUi.Choose(settings, "", new[] { "Evolve a system >", "Run a network >", "Benchmark >", "Settings >", "Quit" }, selection, splash: true);
                selection = choice ?? selection;
                switch (choice)
                {
                    case 0:
                        EvolveMenu();
                        break;
                    case 1:
                        RunMenu();
                        break;
                    case 2:
                        BenchmarkMenu();
                        break;
                    case 3:
                        settings = SettingsMenu.Edit(settings);
                        break;
                    case 4:
                    case null:
                        if (ConsoleUi.Confirm(settings, "Are you sure you wish to quit?"))
                        {
                            return;
                        }
                        break;
                }
            }
        }

        private void EvolveMenu()
        {
            int selection = 0;
            while (ConsoleUi.Choose(settings, "Evolve a system", new[]
                {
                    "Match a target: a set, sequence or binary word...",
                    $"For the selected task: {settings.SelectedTask.Name}",
                    "Starting from the natural numbers network",
                    "Starting from the even numbers network",
                }, selection) is int choice)
            {
                selection = choice;
                switch (choice)
                {
                    case 0:
                        if (SettingsMenu.EditTarget(settings) && SettingsMenu.EditTargetGenerations(settings))
                        {
                            EvolveFromScratch("TargetNet");
                        }
                        break;
                    case 1:
                        EvolveFromScratch("ScratchNet");
                        break;
                    case 2:
                        Evolve("NatNumsNet", "Natural Numbers", TargetTask(), factory => ReferenceNetworks.NaturalNumbers().WithRandomExpressions(factory.NextExpression));
                        break;
                    case 3:
                        Evolve("EvenNumsNet", "Evens", TargetTask(), factory => ReferenceNetworks.EvenNumbers().WithRandomExpressions(factory.NextExpression));
                        break;
                }
            }
        }

        private void RunMenu()
        {
            int selection = 0;
            while (ConsoleUi.Choose(settings, "Run a network", new[] { "Natural numbers network", "Even numbers network", "Import a network from a file..." }, selection) is int choice)
            {
                selection = choice;
                switch (choice)
                {
                    case 0:
                        RunReference("Natural Numbers", ReferenceNetworks.NaturalNumbers());
                        break;
                    case 1:
                        RunReference("Even Numbers", ReferenceNetworks.EvenNumbers());
                        break;
                    case 2:
                        ImportNetwork();
                        break;
                }
            }
        }

        private void BenchmarkMenu()
        {
            int selection = 0;
            while (ConsoleUi.Choose(settings, "Benchmark", new[] { "Every algorithm on the task suite", "Find the best algorithm for the selected task" }, selection) is int choice)
            {
                selection = choice;
                if (choice == 0)
                {
                    RunBenchmark();
                }
                else
                {
                    SelectAlgorithm();
                }
            }
        }

        // The reference networks are generators, so they evolve towards the target whatever task is selected.
        private BenchmarkTask TargetTask() => Catalog.TargetTask.Create(settings) with { RuleForm = settings.RuleForm, Timing = settings.OutputTiming };

        private void EvolveFromScratch(string fileStem)
        {
            if (Catalog.EvolvesRulesOnly(settings.Algorithm) &&
                ConsoleUi.Confirm(settings, $"{settings.Algorithm.Name} keeps a random network's structure. Switch to {Catalog.StructuralDefault.Name}?"))
            {
                settings.Algorithm = Catalog.StructuralDefault;
            }
            Evolve(fileStem, "randomly generated", settings.SelectedTask, factory => factory.NewNetwork());
        }

        private void Evolve(string fileStem, string title, BenchmarkTask task, Func<NetworkFactory, Network> createStartingNetwork)
        {
            string folder = EvolutionSession.NewOutputFolder();
            Console.Clear();
            ConsoleUi.PrintHeader();
            Console.WriteLine(" Evolving a {0} network for: {1}", title, task.Name);
            Console.WriteLine(" The files will be saved to: {0}", folder);
            foreach (string note in EvolutionSession.Notes(settings, task))
            {
                ConsoleUi.WriteLineColoured(ConsoleColor.Yellow, " " + note);
            }
            Console.WriteLine();
            if (!ConsoleUi.WaitForEnterOrEscape(" Press enter to start, or ESC to go back."))
            {
                return;
            }
            IGeneticAlgorithm geneticAlgorithm = EvolutionSession.Evolve(settings, task, createStartingNetwork, random, Console.WriteLine);
            EvolutionSession.Save(geneticAlgorithm, folder, fileStem, Console.WriteLine);
            ConsoleUi.WaitForEnter("Press enter to return to the menu.");
        }

        private void RunBenchmark()
        {
            Console.Clear();
            BenchmarkSettings benchmark = settings.BenchmarkSettings;
            Console.WriteLine("Runs {0} algorithms on {1} tasks, {2} seeds each, with up to {3} evaluations per run, on the {4} engine.",
                AlgorithmCatalog.All.Count, TaskSuite.All.Count, benchmark.Seeds, benchmark.EvaluationBudget, settings.Engine.Name);
            if (!ConsoleUi.WaitForEnterOrEscape("Press enter to start, or ESC to go back; this can take a while."))
            {
                return;
            }
            string folder = EvolutionSession.NewOutputFolder();
            Stopwatch stopwatch = Stopwatch.StartNew();
            IReadOnlyList<BenchmarkRow> rows = Benchmark.Run(AlgorithmCatalog.All, TaskSuite.All, benchmark, Console.WriteLine);
            string table = Benchmark.FormatTable(rows);
            Console.WriteLine("\n{0}\nTime elapsed: {1}", table, stopwatch.Elapsed);
            Directory.CreateDirectory(folder);
            NetworkFiles.SaveText(table, Path.Combine(folder, "benchmark.txt"));
            NetworkFiles.SaveText(Benchmark.FormatCsv(rows), Path.Combine(folder, "benchmark.csv"));
            ConsoleUi.WaitForEnter("Press enter to return to the menu.");
        }

        private void SelectAlgorithm()
        {
            Console.Clear();
            BenchmarkTask task = settings.SelectedTask;
            BenchmarkSettings benchmark = settings.BenchmarkSettings;
            long initialBudget = Math.Max(1, benchmark.EvaluationBudget / 8);
            Console.WriteLine("Successive halving over {0} algorithms for: {1}, starting at {2} evaluations per run.", AlgorithmCatalog.All.Count, task.Name, initialBudget);
            if (!ConsoleUi.WaitForEnterOrEscape("Press enter to start, or ESC to go back; this can take a while."))
            {
                return;
            }
            SelectionResult result = AlgorithmSelector.Select(AlgorithmCatalog.All, task, benchmark, initialBudget, Console.WriteLine);
            Console.WriteLine("\nBest algorithm for {0}: {1}", task.Name, result.Winner.Name);
            if (result.BestFound is Individual best)
            {
                Console.WriteLine("Best network found (fitness {0}, {1}):\n{2}", best.Fitness, best.Description, NetworkNotation.Format(best.Genes));
            }
            ConsoleUi.WaitForEnter("Press enter to continue.");
            if (ConsoleUi.Confirm(settings, $"Use {result.Winner.Name} from now on?"))
            {
                settings.Algorithm = Catalog.Algorithms.First(entry => entry.Name == result.Winner.Name);
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
            ConsoleUi.PromptUntilAccepted("Enter a filename, with its extension, to import from", "Could not load a network from that file.",
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
            ISimulationEngine engine = settings.Engine.Create(settings);
            IReadOnlyList<int> outputs = engine.CollectOutputs(new[] { network }, settings.SimulationOptions, random)[0];
            stopwatch.Stop();
            Console.WriteLine("Final output set: ");
            Console.WriteLine(string.Join("\t", outputs));
            if (network.Neurons.Any(neuron => neuron.IsInput))
            {
                BenchmarkTask task = settings.SelectedTask;
                FitnessResult result = new FitnessEvaluator(engine, task.Task, settings.SimulationOptions, 1, random).Evaluate(network);
                Console.WriteLine("On {0}: fitness {1}, {2}", task.Name, result.Fitness, result.Description);
            }
            else
            {
                PrintSpikeTrain(engine, network);
            }
            ConsoleUi.WaitForEnter($"Time elapsed: {stopwatch.Elapsed}. Press enter to return to the menu.");
        }

        // One run's output spike train, as bits and as the intervals between spikes.
        private void PrintSpikeTrain(ISimulationEngine engine, Network network)
        {
            var trial = new Trial(network, InputSpikes.None, Readout.SpikeTrain);
            IReadOnlyList<int> spikeSteps = engine.Run(new[] { trial }, settings.SimulationOptions with { Repetitions = 1 }, random)[0].SpikeTrains[0];
            Console.WriteLine("One run's spike train over {0} steps:", settings.MaxSteps);
            Console.WriteLine(SpikeTrains.Format(SpikeTrains.Word(spikeSteps, settings.MaxSteps)));
            Console.WriteLine("Intervals between its spikes: {0}", string.Join(",", SpikeTrains.Intervals(spikeSteps)));
        }
    }
}
