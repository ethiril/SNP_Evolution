using System;
using System.Collections.Generic;
using SnpEvolution.Model;

namespace SnpEvolution.Simulation
{
    // What a trial reads from the network.
    public enum Readout
    {
        // The number the output neuron produces; the run stops there.
        Output,

        // Whether the network can halt; the run stops when it does.
        Halting,

        // Every step the output neuron fires on, until MaxSteps or the network halts. The exhaustive engine reports each
        // distinct train once.
        SpikeTrain,

        // Every step each watched neuron fires on and how many spikes it sends, plus every neuron's spikes when the
        // run stops; see PortWatch. The exhaustive engine reports each distinct computation once.
        Ports,
    }

    // The neurons a Ports readout watches, by 1-based position as Neuron.Connections numbers them. The run stops
    // StepsAfterDone steps after the step on which any Done neuron first fires, when the network halts, or at MaxSteps.
    public sealed record PortWatch(IReadOnlyList<int> Neurons, IReadOnlyList<int> Done, int StepsAfterDone)
    {
        public static readonly PortWatch None = new PortWatch(Array.Empty<int>(), Array.Empty<int>(), 0);
    }

    // One network given one input. Watch is what a Ports readout watches (nothing when null), and is ignored otherwise.
    public sealed record Trial(Network Network, InputSpikes Input, Readout Readout, PortWatch? Watch = null)
    {
        public static Trial Generate(Network network) => new Trial(network, InputSpikes.None, Readout.Output);

        // The watch for a Ports readout, which watches nothing when it names no neurons, and null for any other.
        internal PortWatch? PortsWatch => Readout == Readout.Ports ? Watch ?? PortWatch.None : null;
    }
}
