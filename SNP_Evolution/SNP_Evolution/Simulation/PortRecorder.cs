using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Simulation
{
    // What one computation has sent on the neurons a PortWatch names, for a Ports readout.
    internal sealed class PortRecorder
    {
        private readonly PortWatch watch;
        // Each neuron's index in watch.Neurons, or -1 when it is not watched.
        private readonly int[] watchSlot;
        private readonly bool[] isDone;
        private readonly List<Firing>[] firings;
        private int? firstDoneStep;

        public PortRecorder(PortWatch watch, int neuronCount)
        {
            this.watch = watch;
            watchSlot = Enumerable.Repeat(-1, neuronCount).ToArray();
            isDone = new bool[neuronCount];
            firings = new List<Firing>[watch.Neurons.Count];
            for (int slot = 0; slot < watch.Neurons.Count; slot++)
            {
                watchSlot[watch.Neurons[slot] - 1] = slot;
                firings[slot] = new List<Firing>();
            }
            foreach (int position in watch.Done)
            {
                isDone[position - 1] = true;
            }
        }

        private PortRecorder(PortRecorder other)
        {
            watch = other.watch;
            watchSlot = other.watchSlot;
            isDone = other.isDone;
            firings = other.firings.Select(slot => new List<Firing>(slot)).ToArray();
            firstDoneStep = other.firstDoneStep;
        }

        public PortRecorder Clone() => new PortRecorder(this);

        public bool IsOver(int stepCount) => firstDoneStep is int done && stepCount > done + watch.StepsAfterDone;

        // 0-based neuron.
        public void RecordFiring(int neuron, int step, long spikes)
        {
            if (watchSlot[neuron] >= 0)
            {
                firings[watchSlot[neuron]].Add(new Firing(step, spikes));
            }
            if (isDone[neuron] && firstDoneStep == null)
            {
                firstDoneStep = step;
            }
        }

        public PortRun Run(long[] finalSpikes, long[] initialSpikes) =>
            new PortRun(firings.Select(slot => (IReadOnlyList<Firing>)slot.ToArray()).ToArray(), finalSpikes, Array.AsReadOnly(initialSpikes));

        // Negative separators keep the flattening unambiguous, since steps and spike counts are never negative.
        public long[] History()
        {
            var history = new List<long> { firstDoneStep ?? -1 };
            for (int slot = 0; slot < firings.Length; slot++)
            {
                history.Add(-1 - slot);
                foreach (Firing firing in firings[slot])
                {
                    history.Add(firing.Step);
                    history.Add(firing.Spikes);
                }
            }
            return history.ToArray();
        }
    }
}
