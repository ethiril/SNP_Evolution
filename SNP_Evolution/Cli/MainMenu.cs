using System;
using SnpEvolution.Application;

namespace SnpEvolution.Cli
{
    // What every menu shares: the settings in use, which the settings menu and a redone run can replace, and one random
    // source for the session's runs, which are not repeatable.
    internal sealed class MenuState
    {
        public Settings Settings { get; set; } = new Settings();

        public Random Random { get; } = RunSeed.For(null);
    }

    // The top menu groups the work into evolving, running, benchmarking and settings. Every submenu stays open until
    // the user goes back, so several things can be done in a row.
    internal sealed class MainMenu
    {
        private readonly MenuState state = new MenuState();

        public void Run()
        {
            int selection = 0;
            while (true)
            {
                int? choice = ConsoleUi.Choose(state.Settings, "", new[] { "Evolve a system >", "Run a network >", "Benchmark >", "Settings >", "Quit" }, selection, splash: true);
                selection = choice ?? selection;
                switch (choice)
                {
                    case 0:
                        EvolveMenu.Show(state);
                        break;
                    case 1:
                        RunMenu.Show(state);
                        break;
                    case 2:
                        BenchmarkMenu.Show(state);
                        break;
                    case 3:
                        state.Settings = SettingsMenu.Edit(state.Settings);
                        break;
                    case 4:
                    case null:
                        if (ConsoleUi.Confirm(state.Settings, "Are you sure you wish to quit?"))
                        {
                            return;
                        }
                        break;
                }
            }
        }
    }
}
