using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Application;

namespace SnpEvolution.Cli
{
    // The saved runs, newest first. Each can be run again as it was, by the evolve command with its settings, or have
    // its settings loaded to change first.
    internal static class RedoMenu
    {
        public static void Show(MenuState state)
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
                    ConsoleInput.WaitForEnter(" Press enter to return to the menu.");
                    return;
                }
                if (ConsoleUi.Choose(state.Settings, "Redo a saved run", runs.Select(run => ConsoleUi.Row(run.Name, run.Summary)).ToList(), selection) is not int choice)
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
                        state.Settings = chosen.Settings.Copy();
                        CommandScreen.Run(state, new EvolveItem(chosen.Name, Again(chosen)) { SavedName = chosen.Name }, new Dictionary<Option, string>());
                        break;
                    case 1:
                        state.Settings = chosen.Settings.Copy();
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

        // What the saved run evolved for, and the network it started from.
        private static Func<Settings, IEnumerable<(Option, string)>> Again(SavedRun run) => settings => run.Start == RunStart.Scratch
            ? MainMenu.Selected(settings)
            : MainMenu.TargetOf(settings).Append((EvolveCommand.Start, EvolveCommand.Start.Kind.Format(run.Start)));
    }
}
