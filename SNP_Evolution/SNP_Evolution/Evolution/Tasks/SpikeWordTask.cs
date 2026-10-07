using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution.Tasks
{
    // The network runs with no input, and its output spike train should spell the expected binary word: a 1 on every
    // step the output neuron fires, a 0 on every step it does not, from the first step. Scored per run by balanced
    // accuracy over the ones and zeros, so a silent or always-firing network only earns a half.
    public sealed class SpikeWordTask : IPrefixTask, IFocusable
    {
        private const int PrefixBuckets = 16;
        private const int SpikeBuckets = 8;
        private const int FocusLead = 4;
        private const int FocusLength = 8;

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

        public int Length => Expected.Count;

        public ITask Prefix(int length) => new SpikeWordTask(Name, Expected.Take(Math.Clamp(length, 1, Expected.Count)).ToList());

        // Binary words need a few bits before they say anything.
        public CurriculumPlan Curriculum => new CurriculumPlan(Math.Min(8, Length), 4);

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

        // How far into the word the first run is right, by how many times it fires, each as a share of the word.
        public (int, int)? Niche(IReadOnlyList<TrialResult> results)
        {
            if (results[0].SpikeTrains.Count == 0)
            {
                return (0, 0);
            }
            bool[] word = SpikeTrains.Word(results[0].SpikeTrains[0], Expected.Count);
            return (TaskScoring.CorrectPrefix(word, Expected) * PrefixBuckets / Expected.Count, word.Count(bit => bit) * SpikeBuckets / Expected.Count);
        }

        // Each step of the word, scored by the share of runs that get it right.
        public IReadOnlyList<float> Checks(IReadOnlyList<TrialResult> results)
        {
            List<bool[]> words = results[0].SpikeTrains.Select(train => SpikeTrains.Word(train, Expected.Count)).ToList();
            return Enumerable.Range(0, Expected.Count).Select(step => TaskScoring.ShareOfRuns(words, word => word[step] == Expected[step])).ToList();
        }

        public string CheckName(int check) => $"step {check + 1} ({(Expected[check] ? 1 : 0)})";

        // A few steps before the check and a few after, spelled from the first step.
        public ITask? Focus(int check)
        {
            int start = Math.Clamp(check - FocusLead, 0, Math.Max(0, Expected.Count - FocusLength));
            List<bool> window = Expected.Skip(start).Take(FocusLength).ToList();
            return window.Contains(true) ? new SpikeWordTask($"{Name}, steps {start + 1}-{start + window.Count}", window) : null;
        }

        private float ScoreRun(IReadOnlyList<bool> word) =>
            TaskScoring.BalancedAccuracy(Enumerable.Range(0, Expected.Count), step => Expected[step], step => word[step] == Expected[step] ? 1f : 0f);
    }
}
