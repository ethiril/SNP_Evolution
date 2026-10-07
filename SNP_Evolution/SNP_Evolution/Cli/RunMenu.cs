using System;
using System.Diagnostics;
using SnpEvolution.Application;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Storage;

namespace SnpEvolution.Cli
{
    internal static class RunMenu
    {
        public static void Show(MenuState state)
        {
            int selection = 0;
            while (ConsoleUi.Choose(state.Settings, "Run a network", new[] { "Natural numbers network", "Even numbers network", "Import a network from a file..." }, selection) is int choice)
            {
                selection = choice;
                switch (choice)
                {
                    case 0:
                        RunReference(state, "Natural Numbers", ReferenceNetworks.NaturalNumbers());
                        break;
                    case 1:
                        RunReference(state, "Even Numbers", ReferenceNetworks.EvenNumbers());
                        break;
                    case 2:
                        ImportNetwork(state);
                        break;
                }
            }
        }

        private static void RunReference(MenuState state, string title, Network network)
        {
            Console.Clear();
            Console.WriteLine("---------- Running a standard test to get an output from a {0} Network ----------", title);
            RunAndReport(state, network);
        }

        private static void ImportNetwork(MenuState state)
        {
            Network? imported = null;
            ConsoleInput.PromptUntilAccepted("Enter a filename, with its extension, to import from", "Could not load a network from that file.",
                path => (imported = NetworkFiles.Load(path)) != null);
            if (imported == null)
            {
                return;
            }
            Console.WriteLine("\n");
            Console.Write(NetworkNotation.Format(imported));
            RunAndReport(state, imported);
        }

        private static void RunAndReport(MenuState state, Network network)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            NetworkRun run = NetworkRunService.Run(state.Settings, network, state.Random);
            stopwatch.Stop();
            Console.WriteLine("Final output set: ");
            Console.WriteLine(string.Join("\t", run.Outputs));
            if (run.Score != null)
            {
                Console.WriteLine("On {0}: fitness {1}, {2}", run.Task!.Name, run.Score.Fitness, run.Score.Description);
            }
            else
            {
                // One run's output spike train, as bits and as the intervals between spikes.
                int steps = state.Settings.MaxSteps;
                Console.WriteLine("One run's spike train over {0} steps:", steps);
                Console.WriteLine(SpikeTrains.Format(SpikeTrains.Word(run.SpikeSteps!, steps)));
                Console.WriteLine("Intervals between its spikes: {0}", string.Join(",", SpikeTrains.Intervals(run.SpikeSteps!)));
            }
            ConsoleInput.WaitForEnter($"Time elapsed: {stopwatch.Elapsed}. Press enter to return to the menu.");
        }
    }
}
