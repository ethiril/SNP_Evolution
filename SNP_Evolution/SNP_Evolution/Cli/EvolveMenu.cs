using System;
using System.Linq;
using SnpEvolution.Application;
using SnpEvolution.Evolution.Benchmarking;

namespace SnpEvolution.Cli
{
    internal static class EvolveMenu
    {
        public static void Show(MenuState state)
        {
            int selection = 0;
            while (ConsoleUi.Choose(state.Settings, "Evolve a system", new[]
                {
                    "Match a target: a set, sequence or binary word...",
                    $"For the selected task: {state.Settings.SelectedTask.Name}",
                    "Starting from the natural numbers network",
                    "Starting from the even numbers network",
                    $"Suggest settings for: {state.Settings.SelectedTask.Name}",
                    "Compile a target, then shrink it...",
                    "Evolve the first library parts...",
                    "Redo a saved run >",
                }, selection) is int choice)
            {
                selection = choice;
                switch (choice)
                {
                    case 0:
                        if (TargetMenu.EditTarget(state.Settings) && TargetMenu.EditTargetGenerations(state.Settings) && ReviewAdvice(state.Settings))
                        {
                            EvolveFromScratch(state, "TargetNet");
                        }
                        break;
                    case 1:
                        EvolveFromScratch(state, "ScratchNet");
                        break;
                    case 2:
                        Evolve(state, RunStart.NaturalNumbers, "NatNumsNet");
                        break;
                    case 3:
                        Evolve(state, RunStart.EvenNumbers, "EvenNumsNet");
                        break;
                    case 4:
                        ReviewAdvice(state.Settings);
                        ConsoleInput.WaitForEnter(" Press enter to return to the menu.");
                        break;
                    case 5:
                        CompileMenu.CompileAndShrink(state);
                        break;
                    case 6:
                        PartsMenu.EvolveParts(state.Settings);
                        break;
                    case 7:
                        RedoMenu.Show(state);
                        break;
                }
            }
        }

        // Evolves from the given start with the current settings, then offers to save the run under savedName, or a
        // new name when it has none.
        public static void Evolve(MenuState state, RunStart start, string fileStem, string? savedName = null)
        {
            Settings settings = state.Settings;
            BenchmarkTask task = EvolveService.TaskFor(settings, start);
            string folder = RunFolders.NewOutputFolder();
            Console.Clear();
            ConsoleUi.PrintHeader();
            Console.WriteLine(" Evolving a {0} network for: {1}", EvolveService.Title(start), task.Name);
            Console.WriteLine(" The files will be saved to: {0}", folder);
            foreach (string note in RunNotes.For(settings, task))
            {
                ConsoleUi.WriteLineColoured(ConsoleColor.Yellow, " " + note);
            }
            Console.WriteLine();
            if (!ConsoleInput.WaitForEnterOrEscape(" Press enter to start, or ESC to go back."))
            {
                return;
            }
            Settings used = settings.Copy();
            EvolveResult result = EvolveService.Run(new EvolveRequest(settings, state.Random, fileStem, start, folder), Console.WriteLine);
            if (result.Error != null)
            {
                Console.WriteLine(result.Error);
            }
            ConsoleInput.WaitForEnter("Press enter to continue.");
            if (result.Run != null)
            {
                OfferToSave(settings, used, start, fileStem, savedName ?? $"{task.Name} ({DateTime.Now:yyyy-MM-dd HH:mm})", result.Outcome);
            }
        }

        // Shows what the advisor suggests for the selected task and applies it if the user agrees, then offers a pilot
        // that picks the algorithm.
        private static bool ReviewAdvice(Settings settings)
        {
            Advice advice = RunAdvisor.Advise(settings);
            Console.Clear();
            ConsoleUi.PrintHeader();
            ConsoleUi.WriteLineColoured(ConsoleColor.Yellow, " Suggested settings for: " + settings.SelectedTask.Name);
            Console.WriteLine();
            RunAdvisor.Format(advice).ToList().ForEach(line => Console.WriteLine(" " + line));
            Console.WriteLine();
            if (advice.Suggestions.Count > 0)
            {
                if (!ConsoleInput.WaitForEnterOrEscape(" Press enter to apply these suggestions, or ESC to keep the current settings."))
                {
                    Console.WriteLine(" Keeping the current settings.");
                }
                else
                {
                    RunAdvisor.ApplyAll(settings, advice);
                    Console.WriteLine(" Applied.");
                }
            }
            if (ConsoleInput.WaitForEnterOrEscape(" Press enter to run a quick pilot that picks the algorithm, or ESC to skip it."))
            {
                settings.Algorithm = RunAdvisor.Pilot(settings, RunAdvisor.PilotBudget, Console.WriteLine);
                Console.WriteLine(" The pilot picked {0}.", settings.Algorithm.Name);
            }
            return true;
        }

        private static void EvolveFromScratch(MenuState state, string fileStem)
        {
            Settings settings = state.Settings;
            if (settings.Algorithm.EvolvesRulesOnly &&
                ConsoleUi.Confirm(settings, $"{settings.Algorithm.Name} keeps a random network's structure. Switch to {Catalog.StructuralDefault.Name}?"))
            {
                settings.Algorithm = Catalog.StructuralDefault;
            }
            Evolve(state, RunStart.Scratch, fileStem);
        }

        private static void OfferToSave(Settings settings, Settings used, RunStart start, string fileStem, string defaultName, string outcome)
        {
            if (!ConsoleUi.Confirm(settings, "Save this run, to redo it or reuse its settings later?"))
            {
                return;
            }
            string? name = null;
            ConsoleInput.PromptUntilAccepted("Name for this run", "", input => (name = input.Trim().Length > 0 ? input.Trim() : defaultName) != null,
                $"Leave it empty for: {defaultName}", "A saved run with the same name is replaced.", $"Saved runs are kept in {SavedRuns.Default.Path}");
            if (name != null)
            {
                SavedRuns.Default.Add(new SavedRun(name, DateTime.Now, start, fileStem, outcome, used));
            }
        }
    }
}
