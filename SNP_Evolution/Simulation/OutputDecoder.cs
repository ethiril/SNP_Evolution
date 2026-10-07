using System;
using System.Collections.Generic;

namespace SnpEvolution.Simulation
{
    // Turns the output neuron's firings into a number, as OutputTiming says, and keeps the steps it fired on when asked.
    internal sealed class OutputDecoder
    {
        private readonly OutputTiming timing;
        private readonly List<int>? spikeSteps;
        private int counter;
        private bool engaged;

        public OutputDecoder(OutputTiming timing, bool recordSpikeTrain)
        {
            this.timing = timing;
            spikeSteps = recordSpikeTrain ? new List<int>() : null;
        }

        public OutputDecoder(OutputDecoder other)
            : this(other.timing, other.spikeSteps != null)
        {
            spikeSteps?.AddRange(other.spikeSteps!);
            counter = other.counter;
            engaged = other.engaged;
            Output = other.Output;
        }

        // Set once, by the output neuron's second spike.
        public int? Output { get; private set; }

        // The steps (from 0) the output neuron fired on, in order; null unless the spike train is recorded.
        public IReadOnlyList<int>? SpikeSteps => spikeSteps;

        public const int ProgressCount = 3;

        // Writes what decides the rest of the decoding: the count so far, whether the first spike has come, and the output.
        public void WriteProgress(Span<long> key)
        {
            key[0] = counter;
            key[1] = engaged ? 1 : 0;
            key[2] = Output ?? -1;
        }

        public void Record(SpikeRelease release, int step)
        {
            if (release == SpikeRelease.Fired)
            {
                spikeSteps?.Add(step);
            }
            if (Output != null)
            {
                return;
            }
            if (release != SpikeRelease.Fired)
            {
                if (engaged || timing == OutputTiming.Legacy)
                {
                    counter++;
                }
                return;
            }
            if (engaged)
            {
                Output = ++counter;
                return;
            }
            engaged = true;
        }
    }
}
