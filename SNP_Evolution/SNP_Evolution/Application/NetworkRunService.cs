using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Application
{
    // A network's outputs over the settings' runs. A network with inputs is scored on the selected task; one without
    // has one run's output spike train, as the steps it spiked at.
    internal sealed record NetworkRun(IReadOnlyList<int> Outputs, BenchmarkTask? Task, FitnessResult? Score, IReadOnlyList<int>? SpikeSteps);

    // Runs one network with the settings' engine and simulation options, for the menu's Run a network.
    internal static class NetworkRunService
    {
        public static NetworkRun Run(Settings settings, Network network, Random random)
        {
            ISimulationEngine engine = settings.Engine.Create(settings);
            IReadOnlyList<int> outputs = engine.CollectOutputs(new[] { network }, settings.SimulationOptions, random)[0];
            if (network.Neurons.Any(neuron => neuron.IsInput))
            {
                BenchmarkTask task = settings.SelectedTask;
                FitnessResult score = new FitnessEvaluator(engine, task.Task, settings.SimulationOptions, 1, random, new EvaluationBudget()).Evaluate(network);
                return new NetworkRun(outputs, task, score, null);
            }
            var trial = new Trial(network, InputSpikes.None, Readout.SpikeTrain);
            IReadOnlyList<int> spikeSteps = engine.Run(new[] { trial }, settings.SimulationOptions with { Repetitions = 1 }, random)[0].SpikeTrains[0];
            return new NetworkRun(outputs, null, null, spikeSteps);
        }
    }
}
