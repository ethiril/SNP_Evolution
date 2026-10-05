using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Networks;

namespace SnpEvolution.Simulation
{
    public sealed record SimulationOptions(int MaxSteps, int Repetitions, OutputTiming Timing = OutputTiming.Legacy);

    // What a trial reads from the network.
    public enum Readout
    {
        // The number the output neuron produces; the run stops there.
        Output,

        // Whether the network can halt; the run stops when it does.
        Halting,

        // Every step the output neuron fires on, until MaxSteps or the network halts. Always sampled, since each
        // computation has its own train.
        SpikeTrain,
    }

    // One network given one input.
    public sealed record Trial(Network Network, InputSpikes Input, Readout Readout)
    {
        public static Trial Generate(Network network) => new Trial(network, InputSpikes.None, Readout.Output);
    }

    // Outputs is sorted: every sampled run's output, or each possible output once when Exact. CanHalt says whether
    // some computation halted, which for an exact result means whether any computation can halt. SpikeTrains holds
    // each sampled run's output spike steps for a SpikeTrain readout, and is empty otherwise.
    public sealed record TrialResult(IReadOnlyList<int> Outputs, bool CanHalt, bool Exact, IReadOnlyList<IReadOnlyList<int>>? SpikeTrains = null)
    {
        public IReadOnlyList<IReadOnlyList<int>> SpikeTrains { get; init; } = SpikeTrains ?? Array.Empty<IReadOnlyList<int>>();
    }

    // A backend that runs networks. Taking the whole batch lets a backend spread the runs over cores or a GPU.
    public interface ISimulationEngine
    {
        // Returns one result per trial, in order. Engines draw any randomness from random, or seed their own
        // generators from it, so a seeded random makes a batch reproducible.
        IReadOnlyList<TrialResult> Run(IReadOnlyList<Trial> trials, SimulationOptions options, Random random);
    }

    public static class SimulationEngineExtensions
    {
        // Each network's sorted outputs when it runs as a generator, with no input.
        public static IReadOnlyList<IReadOnlyList<int>> CollectOutputs(
            this ISimulationEngine engine, IReadOnlyList<Network> networks, SimulationOptions options, Random random) =>
            engine.Run(networks.Select(Trial.Generate).ToList(), options, random).Select(result => result.Outputs).ToList();
    }
}
