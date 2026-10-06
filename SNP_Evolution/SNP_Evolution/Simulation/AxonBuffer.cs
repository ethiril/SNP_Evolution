using System;

namespace SnpEvolution.Simulation
{
    // Spikes on their way down each neuron's axon, in a ring of MaxAxonalDelay + 1 slots per neuron indexed by the
    // step they leave on.
    internal sealed class AxonBuffer
    {
        private readonly long[] inFlight;
        private readonly int slots;

        public AxonBuffer(int neuronCount, int maxDelay)
        {
            slots = maxDelay + 1;
            inFlight = new long[neuronCount * slots];
        }

        public AxonBuffer(AxonBuffer other)
        {
            slots = other.slots;
            inFlight = new long[other.inFlight.Length];
            other.inFlight.AsSpan().CopyTo(inFlight);
        }

        public bool IsEmpty => Array.TrueForAll(inFlight, spikes => spikes == 0);

        public void Send(int neuron, int leavingStep, long spikes) => inFlight[Slot(neuron, leavingStep)] += spikes;

        // The spikes that leave the neuron's axon on the step, taken out of the buffer.
        public long Leave(int neuron, int step)
        {
            int slot = Slot(neuron, step);
            long leaving = inFlight[slot];
            inFlight[slot] = 0;
            return leaving;
        }

        public int KeyCount => inFlight.Length;

        // Writes the spikes in leaving order from the step, so the same spikes in flight give the same key whatever the step.
        public void WriteKey(Span<long> key, int step)
        {
            for (int neuron = 0; neuron < inFlight.Length / slots; neuron++)
            {
                for (int ahead = 0; ahead < slots; ahead++)
                {
                    key[neuron * slots + ahead] = inFlight[Slot(neuron, step + ahead)];
                }
            }
        }

        private int Slot(int neuron, int step) => neuron * slots + step % slots;
    }
}
