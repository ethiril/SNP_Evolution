using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution.Tasks
{
    // The network runs with no input, and the gaps between its output spikes should be the expected sequence, in
    // order and with repeats, such as 1,1,2,3,5,8. Spikes after the sequence are ignored, so it is a prefix to match.
    // A nondeterministic network is scored on every sampled run, so only one that always gives the sequence solves it.
    public sealed class SequenceTask : ITask
    {
        private const float CloseIntervalCredit = 0.5f;

        public SequenceTask(string name, IReadOnlyList<int> expected)
        {
            Name = name;
            Expected = expected;
        }

        public string Name { get; }

        public IReadOnlyList<int> Expected { get; }

        public int InputCount => 0;

        public IReadOnlyList<TaskCase> Cases { get; } = new[] { new TaskCase(InputSpikes.None, Readout.SpikeTrain) };

        // Room for the whole sequence, plus a margin for the first spike to come late.
        public int StepsNeeded => Expected.Sum() + Math.Max(10, Expected.Sum() / 2);

        public float Score(IReadOnlyList<TrialResult> results) =>
            results[0].SpikeTrains.Count == 0 ? 0 : results[0].SpikeTrains.Average(train => ScoreRun(SpikeTrains.Intervals(train)));

        public string Describe(IReadOnlyList<TrialResult> results)
        {
            IReadOnlyList<IReadOnlyList<int>> trains = results[0].SpikeTrains;
            if (trains.Count == 0)
            {
                return "no runs";
            }
            List<int> first = SpikeTrains.Intervals(trains[0]).Take(Expected.Count + 2).ToList();
            string varies = trains.Any(train => !train.SequenceEqual(trains[0])) ? " (varies between runs)" : "";
            return $"intervals {Format(first)} / {Format(Expected)}{varies}";
        }

        private float ScoreRun(IReadOnlyList<int> intervals) =>
            (float)Expected.Select((expected, index) => index >= intervals.Count ? 0
                : intervals[index] == expected ? 1
                : CloseIntervalCredit / (1 + Math.Abs(intervals[index] - expected))).Average();

        private static string Format(IEnumerable<int> values) => "[" + string.Join(",", values) + "]";
    }
}
