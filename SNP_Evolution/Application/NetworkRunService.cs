using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Model;
using SnpEvolution.Search.Benchmarking;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Application
{
    // A network's outputs over the settings' runs.
    public abstract record NetworkRun(IReadOnlyList<int> Outputs)
    {
        // A network with inputs, scored on the selected task.
        public sealed record Scored(IReadOnlyList<int> Outputs, BenchmarkTask Task, FitnessResult Score) : NetworkRun(Outputs);

        // A network without inputs, with one run's output spike train as the steps it spiked at.
        public sealed record SpikeTrain(IReadOnlyList<int> Outputs, IReadOnlyList<int> SpikeSteps) : NetworkRun(Outputs);
    }

    // Runs one network with the settings' engine and simulation options, for the menu's Run a network.
    public static class NetworkRunService
    {
        public static NetworkRun Run(Settings settings, Network network, Random random)
        {
            ISimulationEngine engine = settings.Engine.Create(settings);
            IReadOnlyList<int> outputs = engine.CollectOutputs(new[] { network }, settings.SimulationOptions, random)[0];
            if (network.Neurons.Any(neuron => neuron.IsInput))
            {
                BenchmarkTask task = settings.SelectedTask;
                FitnessResult score = new FitnessEvaluator(engine, task.Task, settings.SimulationOptions, 1, random, new EvaluationBudget()).Evaluate(network);
                return new NetworkRun.Scored(outputs, task, score);
            }
            var trial = new Trial(network, InputSpikes.None, Readout.SpikeTrain);
            IReadOnlyList<int> spikeSteps = engine.Run(new[] { trial }, settings.SimulationOptions with { Repetitions = 1 }, random)[0].SpikeTrains[0];
            return new NetworkRun.SpikeTrain(outputs, spikeSteps);
        }
    }
}
