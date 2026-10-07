using System;
using System.IO;
using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Genome;
using SnpEvolution.Networks;

namespace SnpEvolution.Cli
{
    internal sealed partial class MainMenu
    {
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
                settings.Algorithm = RunAdvisor.Pilot(settings, PilotBudget, Console.WriteLine);
                Console.WriteLine(" The pilot picked {0}.", settings.Algorithm.Name);
            }
            return true;
        }

        // The reference networks are generators, so they evolve towards the target whatever task is selected.
        private BenchmarkTask TargetTask() => Catalog.TargetTask.Create(settings) with { RuleForm = settings.RuleForm, Timing = settings.OutputTiming };

        private void EvolveFromScratch(string fileStem)
        {
            if (settings.Algorithm.EvolvesRulesOnly &&
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
            EvaluationBudget evaluations = settings.RunBudget();
            IGeneticAlgorithm geneticAlgorithm = EvolutionSession.Evolve(settings, task, createStartingNetwork, random, Console.WriteLine, evaluations);
            RunOutput.Save(geneticAlgorithm, folder, fileStem, Console.WriteLine, evaluations.Report());
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
    }
}
