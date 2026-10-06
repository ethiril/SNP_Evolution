using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Networks;

namespace SnpEvolution.Simulation
{
    // Samples computations of a network with random rule choices.
    public static class NetworkRunner
    {
        internal const int SilentRunsBeforeGivingUp = 7;

        public static int? RunOnce(Network network, int maxSteps, Random random) =>
            RunOnce(new NetworkSimulation(CompiledNetwork.Of(network), random, InputSpikes.None, OutputTiming.Legacy), Readout.Output, maxSteps).Output;

        public static List<int> CollectOutputs(Network network, int maxSteps, int repetitions, Random random) =>
            new List<int>(Sample(Trial.Generate(network), new SimulationOptions(maxSteps, repetitions), random).Outputs);

        // Exactly the given number of runs, with no giving up. Outputs are in run order rather than sorted, for Merge.
        internal static TrialResult SampleRuns(Trial trial, SimulationOptions options, int runs, Random random)
        {
            CompiledNetwork network = CompiledNetwork.Of(trial.Network);
            var outputs = new List<int>();
            var spikeTrains = new List<IReadOnlyList<int>>();
            var portRuns = new List<PortRun>();
            bool recordSpikeTrain = trial.Readout == Readout.SpikeTrain;
            PortWatch? watch = WatchOf(trial);
            bool halted = false;
            for (int run = 0; run < runs; run++)
            {
                var simulation = new NetworkSimulation(network, random, trial.Input, options.Timing, recordSpikeTrain, watch, options.Jitter);
                RunOnce(simulation, trial.Readout, options.MaxSteps);
                if (watch != null)
                {
                    portRuns.Add(simulation.PortRun());
                }
                if (trial.Readout != Readout.Halting && simulation.Output is int output)
                {
                    outputs.Add(output);
                }
                if (recordSpikeTrain)
                {
                    spikeTrains.Add(simulation.OutputSpikeSteps);
                }
                halted |= simulation.IsHalted;
            }
            return new TrialResult(outputs, halted, Exact: false, spikeTrains, portRuns);
        }

        // The trial's watch for a Ports readout, which watches nothing when it names no neurons, and null for any other.
        internal static PortWatch? WatchOf(Trial trial) => trial.Readout == Readout.Ports ? trial.Watch ?? PortWatch.None : null;

        // A trial whose first runs read no output at all is given up on, as it is unlikely ever to produce one.
        public static TrialResult Sample(Trial trial, SimulationOptions options, Random random)
        {
            int opening = Math.Min(options.Repetitions, SilentRunsBeforeGivingUp);
            TrialResult first = SampleRuns(trial, options, opening, random);
            if (trial.Readout == Readout.Output && first.Outputs.Count == 0)
            {
                return first;
            }
            return Merge(new[] { first, SampleRuns(trial, options, options.Repetitions - opening, random) });
        }

        // One result for runs sampled in parts, as if they were sampled together in the given order.
        internal static TrialResult Merge(IEnumerable<TrialResult> parts)
        {
            List<TrialResult> all = parts.ToList();
            return new TrialResult(
                all.SelectMany(part => part.Outputs).OrderBy(output => output).ToList(),
                all.Any(part => part.CanHalt),
                Exact: false,
                all.SelectMany(part => part.SpikeTrains).ToList(),
                all.SelectMany(part => part.PortRuns).ToList());
        }

        private static NetworkSimulation RunOnce(NetworkSimulation simulation, Readout readout, int maxSteps)
        {
            while (simulation.StepCount < maxSteps && !IsOver(simulation, readout))
            {
                simulation.Step();
            }
            return simulation;
        }

        private static bool IsOver(NetworkSimulation simulation, Readout readout) => readout switch
        {
            Readout.Output => simulation.Output != null,
            Readout.Ports => simulation.PortRunOver || simulation.IsHalted,
            _ => simulation.IsHalted,
        };
    }
}
