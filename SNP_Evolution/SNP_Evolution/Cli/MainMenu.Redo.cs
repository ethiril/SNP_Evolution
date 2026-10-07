using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Cli
{
    internal sealed partial class MainMenu
    {
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
    }
}
