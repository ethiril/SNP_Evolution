using System;
using System.Collections.Generic;
using SnpEvolution.Networks;

namespace SnpEvolution.Simulation
{
    // Samples computations of a network with random rule choices.
    public static class NetworkRunner
    {
        private const int SilentRunsBeforeGivingUp = 7;

        public static int? RunOnce(Network network, int maxSteps, Random random) =>
            RunOnce(new NetworkSimulation(CompiledNetwork.Of(network), random, InputSpikes.None, OutputTiming.Legacy), Readout.Output, maxSteps).Output;

        public static List<int> CollectOutputs(Network network, int maxSteps, int repetitions, Random random) =>
            new List<int>(Sample(Trial.Generate(network), new SimulationOptions(maxSteps, repetitions), random).Outputs);

        public static TrialResult Sample(Trial trial, SimulationOptions options, Random random)
        {
            CompiledNetwork network = CompiledNetwork.Of(trial.Network);
            var outputs = new List<int>();
            var spikeTrains = new List<IReadOnlyList<int>>();
            bool recordSpikeTrain = trial.Readout == Readout.SpikeTrain;
            bool halted = false;
            for (int run = 0; run < options.Repetitions; run++)
            {
                var simulation = new NetworkSimulation(network, random, trial.Input, options.Timing, recordSpikeTrain);
                RunOnce(simulation, trial.Readout, options.MaxSteps);
                if (trial.Readout != Readout.Halting && simulation.Output is int output)
                {
                    outputs.Add(output);
                }
                if (recordSpikeTrain)
                {
                    spikeTrains.Add(simulation.OutputSpikeSteps);
                }
                halted |= simulation.IsHalted;
                if (trial.Readout == Readout.Output && outputs.Count == 0 && run + 1 >= SilentRunsBeforeGivingUp)
                {
                    break;
                }
            }
            outputs.Sort();
            return new TrialResult(outputs, halted, Exact: false, spikeTrains);
        }

        private static NetworkSimulation RunOnce(NetworkSimulation simulation, Readout readout, int maxSteps)
        {
            while (simulation.StepCount < maxSteps && !(readout == Readout.Output ? simulation.Output != null : simulation.IsHalted))
            {
                simulation.Step();
            }
            return simulation;
        }
    }
}
