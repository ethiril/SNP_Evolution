using System;

namespace SnpEvolution.Simulation
{
    // Each neuron's own state between steps: the spikes it holds, and what a delayed rule still has it wait for or owe.
    internal sealed class NeuronStates
    {
        public const int KeyLength = 5;

        public readonly long[] Spikes;

        // A holding delay: the steps still to wait, and what the rule did (None when no rule is pending).
        public readonly int[] LegacyDelay;
        public readonly SpikeRelease[] LegacyPending;

        // A closing delay: the steps the neuron stays closed, and the spikes it sends when it opens.
        public readonly int[] ClosedFor;
        public readonly long[] PendingEmission;

        public NeuronStates(ReadOnlySpan<long> initialSpikes)
        {
            Spikes = initialSpikes.ToArray();
            LegacyDelay = new int[Spikes.Length];
            LegacyPending = new SpikeRelease[Spikes.Length];
            ClosedFor = new int[Spikes.Length];
            PendingEmission = new long[Spikes.Length];
        }

        // Copied element by element, which for arrays this small is much quicker than Array.Clone.
        public NeuronStates(NeuronStates other)
        {
            Spikes = Copy(other.Spikes);
            LegacyDelay = Copy(other.LegacyDelay);
            LegacyPending = Copy(other.LegacyPending);
            ClosedFor = Copy(other.ClosedFor);
            PendingEmission = Copy(other.PendingEmission);
        }

        private static T[] Copy<T>(T[] from)
        {
            var to = new T[from.Length];
            from.AsSpan().CopyTo(to);
            return to;
        }

        public bool IsBusy(int neuron) => LegacyDelay[neuron] > 0 || ClosedFor[neuron] > 0;

        public int KeyCount => KeyLength * Spikes.Length;

        // Writes KeyLength values per neuron, in neuron order.
        public void WriteKey(Span<long> key)
        {
            for (int neuron = 0; neuron < Spikes.Length; neuron++)
            {
                int offset = KeyLength * neuron;
                key[offset] = Spikes[neuron];
                key[offset + 1] = LegacyDelay[neuron];
                key[offset + 2] = (long)LegacyPending[neuron];
                key[offset + 3] = ClosedFor[neuron];
                key[offset + 4] = PendingEmission[neuron];
            }
        }
    }
}
