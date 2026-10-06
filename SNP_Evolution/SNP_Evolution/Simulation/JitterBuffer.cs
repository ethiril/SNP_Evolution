using System;

namespace SnpEvolution.Simulation
{
    // Spikes held back by jitter j, in a ring of j + 1 slots per receiving neuron indexed by arrival step. What a neuron
    // sends along each synapse is drawn 0 to j steps late, so the buffer holds the random it draws from.
    internal sealed class JitterBuffer
    {
        private readonly long[] late;
        private readonly int slots;
        private readonly Random random;

        public JitterBuffer(int neuronCount, int jitter, Random random)
        {
            slots = jitter + 1;
            late = new long[neuronCount * slots];
            this.random = random;
        }

        public bool IsEmpty => Array.TrueForAll(late, spikes => spikes == 0);

        // How many steps late the next spikes along a synapse arrive.
        public int DrawLateness() => random.Next(slots);

        public void Hold(int receiver, int arrivalStep, long spikes) => late[Slot(receiver, arrivalStep)] += spikes;

        // The late spikes that reach the receiver on the step, taken out of the buffer.
        public long Arrive(int receiver, int step)
        {
            int slot = Slot(receiver, step);
            long arriving = late[slot];
            late[slot] = 0;
            return arriving;
        }

        // Adds what each neuron still has on its way to it to its spikes.
        public void AddHeldTo(long[] spikes)
        {
            for (int slot = 0; slot < late.Length; slot++)
            {
                spikes[slot / slots] += late[slot];
            }
        }

        private int Slot(int receiver, int step) => receiver * slots + step % slots;
    }
}
