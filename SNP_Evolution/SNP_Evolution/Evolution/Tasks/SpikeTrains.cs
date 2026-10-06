using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Evolution.Tasks
{
    // Readings of an output spike train, given as the steps the output neuron fired on.
    public static class SpikeTrains
    {
        // The gaps between consecutive spikes, so spikes on steps 2, 3, 5 give 1, 2.
        public static List<int> Intervals(IReadOnlyList<int> spikeSteps) =>
            Enumerable.Range(1, System.Math.Max(0, spikeSteps.Count - 1)).Select(index => spikeSteps[index] - spikeSteps[index - 1]).ToList();

        // The first length steps as bits, true where the neuron fired.
        public static bool[] Word(IReadOnlyList<int> spikeSteps, int length)
        {
            var word = new bool[length];
            foreach (int step in spikeSteps)
            {
                if (step < length)
                {
                    word[step] = true;
                }
            }
            return word;
        }

        // How many spikes fall on steps from start up to, but not including, end.
        public static int CountIn(IReadOnlyList<int> spikeSteps, int start, int end) => spikeSteps.Count(step => step >= start && step < end);

        public static string Format(IEnumerable<bool> word) => string.Concat(word.Select(bit => bit ? '1' : '0'));
    }
}
