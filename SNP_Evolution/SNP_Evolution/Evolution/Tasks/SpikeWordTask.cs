using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution.Tasks
{
    // The network runs with no input, and its output spike train should spell the expected binary word: a 1 on every
    // step the output neuron fires, a 0 on every step it does not, from the first step. Scored per run by balanced
    // accuracy over the ones and zeros, so a silent or always-firing network only earns a half.
    public sealed class SpikeWordTask : ITask
    {
        public SpikeWordTask(string name, IReadOnlyList<bool> expected)
        {
            Name = name;
            Expected = expected;
        }

        public string Name { get; }

        public IReadOnlyList<bool> Expected { get; }

        public int InputCount => 0;

        public IReadOnlyList<TaskCase> Cases { get; } = new[] { new TaskCase(InputSpikes.None, Readout.SpikeTrain) };

        public int StepsNeeded => Expected.Count;

        public float Score(IReadOnlyList<TrialResult> results) =>
            results[0].SpikeTrains.Count == 0 ? 0 : results[0].SpikeTrains.Average(train => ScoreRun(SpikeTrains.Word(train, Expected.Count)));

        public string Describe(IReadOnlyList<TrialResult> results)
        {
            IReadOnlyList<IReadOnlyList<int>> trains = results[0].SpikeTrains;
            if (trains.Count == 0)
            {
                return "no runs";
            }
            string varies = trains.Any(train => !SpikeTrains.Word(train, Expected.Count).SequenceEqual(SpikeTrains.Word(trains[0], Expected.Count)))
                ? " (varies between runs)" : "";
            return $"spikes {SpikeTrains.Format(SpikeTrains.Word(trains[0], Expected.Count))} / {SpikeTrains.Format(Expected)}{varies}";
        }

        private float ScoreRun(IReadOnlyList<bool> word)
        {
            float[] accuracy = new[] { true, false }
                .Select(bit => Enumerable.Range(0, Expected.Count).Where(step => Expected[step] == bit).ToList())
                .Where(steps => steps.Count > 0)
                .Select(steps => (float)steps.Count(step => word[step] == Expected[step]) / steps.Count)
                .ToArray();
            return accuracy.Average();
        }
    }
}
