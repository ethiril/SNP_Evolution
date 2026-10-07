using System;

namespace SnpEvolution.Cli
{
    // The top menu groups the work into evolving, running, benchmarking and settings. Every submenu stays open until
    // the user goes back, so several things can be done in a row.
    internal sealed partial class MainMenu
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
    }
}
