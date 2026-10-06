using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution.Verification
{
    // How well a part keeps its contract when what each neuron sends along each synapse may arrive up to j steps late
    // (SimulationOptions.Jitter), as on asynchronous hardware: the share of sampled runs, each running every case once, in
    // which every case still stays quiet before start, fires the right done once with the right outputs, and leaves every
    // neuron with the spikes it began with. Latency bounds are not checked, since jitter changes timing by design, which is
    // what a time-free SN P system asks of its result. Runs get 1 + j times the steps a lockstep run has, for the delays.
    public static class Robustness
    {
        public const int Runs = 100;

        // Fewer runs per network when robustness picks MAP-Elites cells, where every child is measured.
        public const int CellRuns = 20;

        // The jitters the evolve-parts summary reports.
        public static IReadOnlyList<int> Reported { get; } = new[] { 1, 2 };

        public static float Of(Part part, int jitter, int runs = Runs, int seed = 1) => Of(part.Network, part.Task(), jitter, runs, seed);

        public static float Of(Network network, ContractTask task, int jitter, int runs = Runs, int seed = 1)
        {
            int slack = 1 + jitter;
            List<Trial> trials = task.Cases
                .Select(@case => new Trial(network, @case.Input, @case.Readout, @case.Watch! with { StepsAfterDone = @case.Watch.StepsAfterDone * slack }))
                .ToList();
            var options = new SimulationOptions(task.StepsNeeded * slack, runs, OutputTiming.Interval, jitter);
            IReadOnlyList<TrialResult> results = new ParallelCpuEngine().Run(trials, options, new Random(seed));
            int kept = Enumerable.Range(0, runs).Count(run => results.Select((result, caseIndex) => task.Keeps(result.PortRuns[run], caseIndex, timed: false)).All(keeps => keeps));
            return (float)kept / runs;
        }

        // A MAP-Elites cell of robustness in tenths by neuron count, so a run keeps the best network at every level of
        // robustness and selection can work towards parts that survive jitter. Each network is measured once.
        public static Func<Network, (int, int)> Cells(ContractTask task, int jitter, int runs = CellRuns)
        {
            var measured = new ConditionalWeakTable<Network, StrongBox<float>>();
            return network => (Tenths(measured.GetValue(network, _ => new StrongBox<float>(Of(network, task, jitter, runs))).Value), network.Neurons.Count);
        }

        public static int Tenths(float robustness) => (int)Math.Floor(robustness * 10 + 1e-4);
    }
}
