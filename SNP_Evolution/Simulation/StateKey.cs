using System;
using System.Collections.Generic;

namespace SnpEvolution.Simulation
{
    // Everything that decides the future of a computation, so equal keys at the same step can be merged. Without the
    // output's progress, it is everything that decides whether the computation can halt. A recorded spike train and a
    // Ports record are part of the key, so computations with different records so far are never merged. The exhaustive
    // engine makes one for every configuration it reaches, so it is written straight into one array.
    internal static class StateKey
    {
        public static long[] Of(NeuronStates neurons, OutputDecoder? progress, AxonBuffer? axon, int step, IReadOnlyList<int>? spikeSteps, PortRecorder? ports)
        {
            long[]? history = ports?.History();
            var key = new long[neurons.KeyCount + (progress == null ? 0 : OutputDecoder.ProgressCount) + (axon?.KeyCount ?? 0)
                + (spikeSteps == null ? 0 : 1 + spikeSteps.Count) + (history?.Length ?? 0)];
            Span<long> rest = key;
            neurons.WriteKey(rest);
            rest = rest[neurons.KeyCount..];
            if (progress != null)
            {
                progress.WriteProgress(rest);
                rest = rest[OutputDecoder.ProgressCount..];
            }
            if (axon != null)
            {
                axon.WriteKey(rest, step);
                rest = rest[axon.KeyCount..];
            }
            if (spikeSteps != null)
            {
                rest[0] = -1;
                for (int index = 0; index < spikeSteps.Count; index++)
                {
                    rest[1 + index] = spikeSteps[index];
                }
                rest = rest[(1 + spikeSteps.Count)..];
            }
            history?.CopyTo(rest);
            return key;
        }
    }
}
