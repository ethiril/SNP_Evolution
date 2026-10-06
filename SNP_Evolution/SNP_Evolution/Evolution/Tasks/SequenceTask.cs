using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution.Tasks
{
    // The network runs with no input, and the gaps between its output spikes should be the expected sequence, in
    // order and with repeats, such as 1,1,2,3,5,8. Spikes after the sequence are ignored, so it is a prefix to match.
    // A run earns credit for how many gaps it gets right before its first mistake, plus a little for being close on
    // that first wrong gap; anything after a mistake earns nothing, since a right value in the wrong place is no
    // step towards the sequence. A nondeterministic network is scored on the mean and the worst of its sampled runs,
    // so a network that gives the sequence every time beats one that only sometimes does.
    public sealed class SequenceTask : IPrefixTask
    {
        private const int MaxGapBucket = 12;
        private const int FocusLength = 3;

        public SequenceTask(string name, IReadOnlyList<int> expected)
        {
            Name = name;
            Expected = expected;
        }

        public string Name { get; }

        public IReadOnlyList<int> Expected { get; }

        public int Length => Expected.Count;

        public int InputCount => 0;

        public IReadOnlyList<TaskCase> Cases { get; } = new[] { new TaskCase(InputSpikes.None, Readout.SpikeTrain) };

        // Room for the whole sequence, plus a margin for the first spike to come late.
        public int StepsNeeded => Expected.Sum() + Math.Max(10, Expected.Sum() / 2);

        public ITask Prefix(int length) => new SequenceTask(Name, Expected.Take(Math.Clamp(length, 1, Expected.Count)).ToList());

        public float Score(IReadOnlyList<TrialResult> results)
        {
            IReadOnlyList<IReadOnlyList<int>> trains = results[0].SpikeTrains;
            if (trains.Count == 0)
            {
                return 0;
            }
            float[] runs = trains.Select(train => ScoreRun(SpikeTrains.Intervals(train))).ToArray();
            return (runs.Average() + runs.Min()) / 2;
        }

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

        // How many gaps the first run gets right before its first mistake, by the longest gap among the ones it
        // makes, in powers of two, so a network that can already wait long enough survives before it waits at the
        // right moment.
        public (int, int)? Niche(IReadOnlyList<TrialResult> results)
        {
            if (results[0].SpikeTrains.Count == 0)
            {
                return (0, 0);
            }
            List<int> intervals = SpikeTrains.Intervals(results[0].SpikeTrains[0]).Take(Expected.Count + 2).ToList();
            int longest = intervals.Count == 0 ? 0 : intervals.Max();
            int bucket = longest == 0 ? 0 : Math.Min(MaxGapBucket, (int)Math.Log2(longest) + 1);
            return (CorrectPrefix(intervals), bucket);
        }

        // Each gap, in its place, scored by the share of runs that get it right. Unlike the fitness this counts a gap
        // even after an earlier mistake, so a network that can already make the later gaps stands out.
        public IReadOnlyList<float> Checks(IReadOnlyList<TrialResult> results)
        {
            IReadOnlyList<IReadOnlyList<int>> trains = results[0].SpikeTrains;
            var checks = new float[Expected.Count];
            foreach (IReadOnlyList<int> train in trains)
            {
                List<int> intervals = SpikeTrains.Intervals(train).Take(Expected.Count).ToList();
                for (int gap = 0; gap < intervals.Count; gap++)
                {
                    checks[gap] += intervals[gap] == Expected[gap] ? 1f / trains.Count : 0;
                }
            }
            return checks;
        }

        public string CheckName(int check) => $"gap {check + 1} ({Expected[check]})";

        // The gap before the check and the two after it, so a part evolved for them starts from a gap the main
        // networks already make.
        // The first gap nobody makes, as a timer: done fires that many steps after start.
        public Contract? Propose(IReadOnlyList<int> unsolvedChecks) =>
            unsolvedChecks.Count > 0 ? ReferenceParts.DelayContract(Expected[unsolvedChecks[0]]) : null;

        public ITask? Focus(int check)
        {
            int start = Math.Clamp(check - 1, 0, Expected.Count - 1);
            return new SequenceTask($"{Name}, gaps {start + 1}-{Math.Min(Expected.Count, start + FocusLength)}", Expected.Skip(start).Take(FocusLength).ToList());
        }

        // The same gaps as Focus, made after a trigger: the part's input receives one spike, and its output should
        // then fire as if the step after the spike arrived had been the last spike of the sequence so far.
        public ITask? Triggered(int check)
        {
            int start = Math.Clamp(check, 0, Expected.Count - 1);
            return new TriggeredSequenceTask($"{Name}, gaps {start + 1}-{Math.Min(Expected.Count, start + FocusLength)} after a trigger",
                Expected.Skip(start).Take(FocusLength).ToList());
        }

        public int CorrectPrefix(IReadOnlyList<int> intervals)
        {
            int prefix = 0;
            while (prefix < Expected.Count && prefix < intervals.Count && intervals[prefix] == Expected[prefix])
            {
                prefix++;
            }
            return prefix;
        }

        private float ScoreRun(IReadOnlyList<int> intervals)
        {
            int prefix = CorrectPrefix(intervals);
            float close = prefix < Expected.Count && prefix < intervals.Count
                ? CloseCredit.Score(intervals[prefix], Expected[prefix])
                : 0;
            return (prefix + close) / Expected.Count;
        }

        private static string Format(IEnumerable<int> values) => "[" + string.Join(",", values) + "]";
    }
}
