using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;

namespace SnpEvolution.Cli
{
    internal sealed partial class MainMenu
    {
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
                FitnessResult result = new FitnessEvaluator(engine, task.Task, settings.SimulationOptions, 1, random, new EvaluationBudget()).Evaluate(network);
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
