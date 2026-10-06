using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Contracts;
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
                    $"Suggest settings for: {settings.SelectedTask.Name}",
                    "Compile a target, then shrink it...",
                    "Evolve the first library parts...",
                    "Redo a saved run >",
                }, selection) is int choice)
            {
                selection = choice;
                switch (choice)
                {
                    case 0:
                        if (TargetMenu.EditTarget(settings) && TargetMenu.EditTargetGenerations(settings) && ReviewAdvice())
                        {
                            EvolveFromScratch("TargetNet");
                        }
                        break;
                    case 1:
                        EvolveFromScratch("ScratchNet");
                        break;
                    case 2:
                        Evolve(RunStart.NaturalNumbers, "NatNumsNet");
                        break;
                    case 3:
                        Evolve(RunStart.EvenNumbers, "EvenNumsNet");
                        break;
                    case 4:
                        ReviewAdvice();
                        ConsoleUi.WaitForEnter(" Press enter to return to the menu.");
                        break;
                    case 5:
                        CompileAndShrink();
                        break;
                    case 6:
                        EvolveParts();
                        break;
                    case 7:
                        RedoMenu();
                        break;
                }
            }
        }

        // Builds a network that is correct by construction for a sequence or set target, then evolves it smaller
        // (see CompileSession). A set needs a register program, which is evolved unless the user gives a file.
        private void CompileAndShrink()
        {
            const int DefaultShrinkGenerations = 300;
            const int ProgramGenerations = 2000;
            if (!TargetMenu.EditTarget(settings))
            {
                return;
            }
            if (settings.Target.Kind == TargetKind.BinaryWord)
            {
                Console.Clear();
                ConsoleUi.PrintHeader();
                Console.WriteLine(" Only sequence and set targets can be compiled.");
                ConsoleUi.WaitForEnter(" Press enter to return to the menu.");
                return;
            }
            string? programFile = null;
            bool accepted = settings.Target.Kind == TargetKind.Sequence;
            if (!accepted)
            {
                ConsoleUi.PromptUntilAccepted("Register program file, or leave it empty to evolve one", "That file does not exist.", input =>
                {
                    programFile = input.Trim().Length > 0 ? input.Trim() : null;
                    return accepted = programFile == null || File.Exists(programFile);
                }, $"Target: {settings.Target.Kind} {settings.Target}", "One instruction per line, such as: 0: ADD r1 -> 1 | 2, 1: SUB r1 -> 1 else 2, 2: HALT",
                    "Register 0 is the output and can only be added to.");
            }
            if (!accepted)
            {
                return;
            }
            int shrinkGenerations = DefaultShrinkGenerations;
            accepted = false;
            ConsoleUi.PromptUntilAccepted("Generations to shrink the compiled network for", "Number was not a whole number of 0 or more.", input =>
            {
                if (input.Trim().Length == 0)
                {
                    return accepted = true;
                }
                return accepted = InputParsing.TryNonNegativeInt(input.Trim(), out shrinkGenerations);
            }, $"Target: {settings.Target.Kind} {settings.Target}", $"Leave it empty for {DefaultShrinkGenerations}, or 0 to only compile.");
            if (!accepted)
            {
                return;
            }
            Console.Clear();
            ConsoleUi.PrintHeader();
            Console.WriteLine(" Compiling {0} {1}, then shrinking it for {2} generations.", settings.Target.Kind, settings.Target, shrinkGenerations);
            Console.WriteLine(settings.Target.Kind == TargetKind.Sequence
                ? " The gaps are fitted with a recurrence, which is compiled into a network that makes them exactly."
                : programFile != null
                    ? $" The program in {programFile} is compiled with the standard ADD and SUB modules."
                    : $" A register program is evolved for up to {ProgramGenerations} generations, then compiled with the standard ADD and SUB modules.");
            Console.WriteLine();
            if (!ConsoleUi.WaitForEnterOrEscape(" Press enter to start, or ESC to go back."))
            {
                return;
            }
            CompileSession.Run(settings, new CompileSession.Options(shrinkGenerations, ProgramGenerations, programFile), random, Console.WriteLine);
            ConsoleUi.WaitForEnter("Press enter to continue.");
        }

        // Runs evolve-parts on every first-part contract the library folder has no part for (see PartsSession).
        private void EvolveParts()
        {
            int seed = 1;
            bool accepted = false;
            ConsoleUi.PromptUntilAccepted("Seed", "Number was not a whole number of 1 or more.", input =>
            {
                if (input.Trim().Length == 0)
                {
                    return accepted = true;
                }
                return accepted = InputParsing.TryNonNegativeInt(input.Trim(), out seed) && seed >= 1;
            }, $"Library folder: {settings.PartLibraryFolder}", "The same seed always evolves the same parts. Leave it empty for 1.");
            if (!accepted)
            {
                return;
            }
            Console.Clear();
            ConsoleUi.PrintHeader();
            Console.WriteLine(" Evolving a part for each of the {0} first-part contracts the library has no part for, with seed {1}.", FirstParts.Contracts.Count, seed);
            Console.WriteLine(" Each is verified on the exhaustive engine, shrunk on hardware cost and saved to {0}.", settings.PartLibraryFolder);
            Console.WriteLine(" Up to {0} evaluations are spent searching for each part.", settings.PartBudget);
            Console.WriteLine();
            if (!ConsoleUi.WaitForEnterOrEscape(" Press enter to start, or ESC to go back."))
            {
                return;
            }
            var options = new PartsSession.Options(seed, settings.PartBudget, FirstParts.Contracts, settings.PartLibraryFolder, () => new ExhaustiveCpuEngine(), Redo: false);
            PartsSession.Run(options, Console.WriteLine);
            ConsoleUi.WaitForEnter("Press enter to continue.");
        }

        // The saved runs, newest first. Each can be run again as it was, or have its settings loaded to change first.
        private void RedoMenu()
        {
            int selection = 0;
            while (true)
            {
                IReadOnlyList<SavedRun> runs = SavedRuns.Default.Load();
                if (runs.Count == 0)
                {
                    Console.Clear();
                    ConsoleUi.PrintHeader();
                    Console.WriteLine(" No saved runs yet. When a run finishes you can save it, and it will be listed here.");
                    ConsoleUi.WaitForEnter(" Press enter to return to the menu.");
                    return;
                }
                if (ConsoleUi.Choose(settings, "Redo a saved run", runs.Select(run => ConsoleUi.Row(run.Name, run.Summary)).ToList(), selection) is not int choice)
                {
                    return;
                }
                selection = choice;
                SavedRun chosen = runs[choice];
                // The status lines show the saved run's settings rather than the current ones.
                switch (ConsoleUi.Choose(chosen.Settings, $"Saved run: {chosen.Name} ({chosen.Outcome})",
                    new[] { "Run it again", "Load its settings, to change them before running", "Delete it" }))
                {
                    case 0:
                        settings = chosen.Settings.Copy();
                        Evolve(chosen.Start, chosen.FileStem, chosen.Name);
                        break;
                    case 1:
                        settings = chosen.Settings.Copy();
                        return;
                    case 2:
                        if (ConsoleUi.Confirm(chosen.Settings, $"Delete the saved run {chosen.Name}?"))
                        {
                            SavedRuns.Default.Remove(chosen.Name);
                            selection = 0;
                        }
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

        // Shows what the advisor suggests for the selected task and applies it if the user agrees, then offers a pilot
        // that picks the algorithm. False when the user backs out at the first question.
        private bool ReviewAdvice()
        {
            const long PilotBudget = 500;
            Advice advice = RunAdvisor.Advise(settings);
            Console.Clear();
            ConsoleUi.PrintHeader();
            ConsoleUi.WriteLineColoured(ConsoleColor.Yellow, " Suggested settings for: " + settings.SelectedTask.Name);
            Console.WriteLine();
            foreach (string line in RunAdvisor.Format(advice))
            {
                Console.WriteLine(" " + line);
            }
            Console.WriteLine();
            if (advice.Suggestions.Count > 0)
            {
                if (!ConsoleUi.WaitForEnterOrEscape(" Press enter to apply these suggestions, or ESC to keep the current settings."))
                {
                    Console.WriteLine(" Keeping the current settings.");
                }
                else
                {
                    RunAdvisor.ApplyAll(settings, advice);
                    Console.WriteLine(" Applied.");
                }
            }
            if (ConsoleUi.WaitForEnterOrEscape(" Press enter to run a quick pilot that picks the algorithm, or ESC to skip it."))
            {
                AlgorithmChoice winner = RunAdvisor.Pilot(settings, PilotBudget, Console.WriteLine);
                settings.Algorithm = Catalog.Algorithms.First(entry => entry.Name == winner.Name);
                Console.WriteLine(" The pilot picked {0}.", winner.Name);
            }
            return true;
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
            Evolve(RunStart.Scratch, fileStem);
        }

        // Evolves from the given start with the current settings, then offers to save the run under savedName, or a
        // new name when it has none.
        private void Evolve(RunStart start, string fileStem, string? savedName = null)
        {
            BenchmarkTask task = start == RunStart.Scratch ? settings.SelectedTask : TargetTask();
            (string title, Func<NetworkFactory, Network> createStartingNetwork) = start switch
            {
                RunStart.NaturalNumbers => ("Natural Numbers", factory => ReferenceNetworks.NaturalNumbers().WithRandomExpressions(factory.NextExpression)),
                RunStart.EvenNumbers => ("Evens", factory => ReferenceNetworks.EvenNumbers().WithRandomExpressions(factory.NextExpression)),
                _ => ("randomly generated", (Func<NetworkFactory, Network>)(factory => factory.NewNetwork())),
            };
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
            Settings used = settings.Copy();
            var evaluations = new EvaluationCounter();
            IGeneticAlgorithm geneticAlgorithm = EvolutionSession.Evolve(settings, task, createStartingNetwork, random, Console.WriteLine, evaluations);
            RunOutput.Save(geneticAlgorithm, folder, fileStem, Console.WriteLine, evaluations);
            ConsoleUi.WaitForEnter("Press enter to continue.");
            OfferToSave(used, start, fileStem, savedName ?? $"{task.Name} ({DateTime.Now:yyyy-MM-dd HH:mm})", Outcome(geneticAlgorithm));
        }

        private void OfferToSave(Settings used, RunStart start, string fileStem, string defaultName, string outcome)
        {
            if (!ConsoleUi.Confirm(settings, "Save this run, to redo it or reuse its settings later?"))
            {
                return;
            }
            string? name = null;
            ConsoleUi.PromptUntilAccepted("Name for this run", "", input => (name = input.Trim().Length > 0 ? input.Trim() : defaultName) != null,
                $"Leave it empty for: {defaultName}", "A saved run with the same name is replaced.", $"Saved runs are kept in {SavedRuns.Default.Path}");
            if (name != null)
            {
                SavedRuns.Default.Add(new SavedRun(name, DateTime.Now, start, fileStem, outcome, used));
            }
        }

        private static string Outcome(IGeneticAlgorithm geneticAlgorithm)
        {
            string fitness = geneticAlgorithm.Best is Individual best ? $"best fitness {best.Fitness:0.###}" : "nothing evaluated";
            return EvolutionSession.IsSolved(geneticAlgorithm) ? $"solved, {fitness}"
                : geneticAlgorithm is IterativeEvolution iterative ? $"stage {iterative.Stage + 1}/{iterative.StageCount}, {fitness}"
                : fitness;
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
