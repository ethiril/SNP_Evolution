using System;
using System.Collections.Generic;

namespace SnpEvolution.Simulation
{
    // How much of a trial's computations a result covers.
    public enum TrialCoverage
    {
        // Every computation, followed by the exhaustive engine.
        Exact,

        // Computations sampled at random.
        Sampled,

        // Too many computations to follow, so the exhaustive engine sampled some instead.
        TooWide,

        // Nothing: the engine cannot run the trial (see EngineSupport).
        Unsupported,
    }

    // A watched neuron sending spikes on a step (from 0).
    public readonly record struct Firing(int Step, long Spikes);

    // One computation as a Ports readout sees it: each watched neuron's firings in step order, in PortWatch.Neurons
    // order, every neuron's spikes when the run stopped and when it began, and the most spikes any neuron held on any
    // step, which is how wide a register hardware needs.
    public sealed record PortRun(IReadOnlyList<IReadOnlyList<Firing>> Firings, IReadOnlyList<long> FinalSpikes, IReadOnlyList<long> InitialSpikes, long MostHeld = 0);

    // Outputs is sorted: every sampled run's output, or each possible output once when Exact. CanHalt says whether
    // some computation halted, which for an exact result means whether any computation can halt. SpikeTrains holds
    // each sampled run's output spike steps for a SpikeTrain readout, or each distinct train once when Exact, and is
    // empty otherwise. PortRuns holds each sampled run, or each distinct computation once when Exact, for a Ports
    // readout, and is empty otherwise.
    public sealed record TrialResult(IReadOnlyList<int> Outputs, bool CanHalt, TrialCoverage Coverage, IReadOnlyList<IReadOnlyList<int>>? SpikeTrains = null,
        IReadOnlyList<PortRun>? PortRuns = null)
    {
        public static readonly TrialResult Unsupported = new TrialResult(Array.Empty<int>(), false, TrialCoverage.Unsupported);

        public IReadOnlyList<IReadOnlyList<int>> SpikeTrains { get; init; } = SpikeTrains ?? Array.Empty<IReadOnlyList<int>>();

        public IReadOnlyList<PortRun> PortRuns { get; init; } = PortRuns ?? Array.Empty<PortRun>();

        public bool Exact => Coverage == TrialCoverage.Exact;
    }
}
