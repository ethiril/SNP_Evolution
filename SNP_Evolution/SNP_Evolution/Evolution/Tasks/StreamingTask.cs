using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution.Tasks
{
    // What the output should do within one window of a streaming case.
    public enum WindowTarget
    {
        // No output spike.
        Silent,

        // Exactly one output spike.
        Once,

        // At least one output spike.
        Active,
    }

    // Steps from Start up to, but not including, End.
    public sealed record StreamWindow(int Start, int End, WindowTarget Target)
    {
        public bool WantsOutput => Target != WindowTarget.Silent;

        public bool Passes(IReadOnlyList<int> outputSteps)
        {
            int fired = SpikeTrains.CountIn(outputSteps, Start, End);
            return Target switch
            {
                WindowTarget.Silent => fired == 0,
                WindowTarget.Once => fired == 1,
                _ => fired >= 1,
            };
        }
    }

    // One sensor train and the windows its output is judged on. Steps outside every window are not judged.
    public sealed record StreamCase(IReadOnlyList<int> InputSteps, int Length, IReadOnlyList<StreamWindow> Windows);

    // A controller's task: a long train of sensor spikes arrives on the one input neuron, with no start or done, and the
    // output's spike train is judged window by window, since a controller is judged on its behaviour over time rather than
    // on any one step. Each window is one check. Scored by balanced accuracy over the windows that want output and those
    // that want silence, so a silent network earns a half and one that copies its input less. Inputs are drawn from a
    // fixed seed per case, so every run of every network sees the same trains.
    public sealed class StreamingTask : ITask
    {
        public const int DefaultCaseCount = 4;
        public const int DefaultSeed = 1;

        // A debouncer's window runs on this long past its burst, so a slower part has time to answer.
        private const int SettleSteps = 9;
        private const int Segments = 8;
        private const int WindowsPerRateSegment = 3;

        public StreamingTask(string name, IReadOnlyList<StreamCase> streams)
        {
            Name = name;
            Streams = streams;
            Cases = streams.Select(stream => new TaskCase(InputSpikes.Train(stream.InputSteps), Readout.SpikeTrain)).ToList();
            StepsNeeded = streams.Max(stream => stream.Length);
            windows = streams.SelectMany((stream, caseIndex) => stream.Windows.Select(window => (caseIndex, window))).ToList();
        }

        private readonly List<(int Case, StreamWindow Window)> windows;

        public string Name { get; }

        public IReadOnlyList<StreamCase> Streams { get; }

        public int InputCount => 1;

        public IReadOnlyList<TaskCase> Cases { get; }

        public int StepsNeeded { get; }

        // A controller that misjudges one window in a long train is still wrong, and with many windows one miss would score above the usual threshold.
        public float SolvedFitness => 1f;

        // Bursts of at least spikes input spikes within the given steps, each answered by exactly one output spike before
        // the next burst can start, among glitches of fewer spikes and quiet stretches, which get no answer. A burst or
        // glitch sits in the first steps of its window and the window runs on for SettleSteps more.
        public static StreamingTask Debouncer(int spikes, int within, int caseCount = DefaultCaseCount, int seed = DefaultSeed)
        {
            if (spikes < 2 || within < spikes)
            {
                throw new ArgumentException("A debouncer needs at least 2 spikes, within at least as many steps.");
            }
            int window = within + SettleSteps;
            return new StreamingTask($"Debounce {spikes} spikes in {within} steps", Enumerable.Range(0, caseCount).Select(caseIndex =>
            {
                var random = new Random(unchecked(seed * 7919 + caseIndex));
                var input = new List<int>();
                var windows = new List<StreamWindow>();
                for (int segment = 0; segment < Segments; segment++)
                {
                    int start = segment * window;
                    double kind = random.NextDouble();
                    int count = kind < 0.4 ? random.Next(spikes, within + 1) : kind < 0.8 ? random.Next(1, spikes) : 0;
                    input.AddRange(Enumerable.Range(start, within).OrderBy(_ => random.Next()).Take(count));
                    windows.Add(new StreamWindow(start, start + window, count >= spikes ? WindowTarget.Once : WindowTarget.Silent));
                }
                return new StreamCase(input, Segments * window, windows);
            }).ToList());
        }

        // Stretches of steady input, each WindowsPerRateSegment windows long: fast stretches send a spike every 1 to
        // within / spikes steps, so at least spikes per window, and slow ones every 2 within / spikes to within steps, or
        // none. The output fires at least once in every window of a fast stretch and never in a slow one. The first window
        // of each stretch is not judged, as the rate has only just changed.
        public static StreamingTask RateDetector(int spikes, int within, int caseCount = DefaultCaseCount, int seed = DefaultSeed)
        {
            int fastestSlow = 2 * within / spikes;
            if (spikes < 2 || within / spikes < 1 || fastestSlow > within)
            {
                throw new ArgumentException("A rate detector needs at least 2 spikes, and a window at least as long as the spikes.");
            }
            int stretch = within * WindowsPerRateSegment;
            return new StreamingTask($"Detect rate {spikes} per {within} steps", Enumerable.Range(0, caseCount).Select(caseIndex =>
            {
                var random = new Random(unchecked(seed * 104729 + caseIndex));
                var input = new List<int>();
                var windows = new List<StreamWindow>();
                for (int segment = 0; segment < Segments; segment++)
                {
                    int start = segment * stretch;
                    bool fast = random.Next(2) == 0;
                    int period = fast ? random.Next(1, within / spikes + 1) : random.Next(4) == 0 ? 0 : random.Next(fastestSlow, within + 1);
                    if (period > 0)
                    {
                        for (int step = start + random.Next(period); step < start + stretch; step += period)
                        {
                            input.Add(step);
                        }
                    }
                    windows.AddRange(Enumerable.Range(1, WindowsPerRateSegment - 1)
                        .Select(index => new StreamWindow(start + index * within, start + (index + 1) * within, fast ? WindowTarget.Active : WindowTarget.Silent)));
                }
                return new StreamCase(input, Segments * stretch, windows);
            }).ToList());
        }

        public float Score(IReadOnlyList<TrialResult> results)
        {
            IReadOnlyList<float> checks = Checks(results);
            float[] accuracy = new[] { true, false }
                .Select(wantsOutput => Enumerable.Range(0, windows.Count).Where(check => windows[check].Window.WantsOutput == wantsOutput).ToList())
                .Where(group => group.Count > 0)
                .Select(group => group.Average(check => checks[check]))
                .ToArray();
            return accuracy.Length == 0 ? 0 : accuracy.Average();
        }

        // Each window, scored by the share of runs whose output does the right thing in it.
        public IReadOnlyList<float> Checks(IReadOnlyList<TrialResult> results) =>
            windows.Select(pair =>
            {
                IReadOnlyList<IReadOnlyList<int>> trains = results[pair.Case].SpikeTrains;
                return trains.Count == 0 ? 0 : (float)trains.Count(train => pair.Window.Passes(train)) / trains.Count;
            }).ToList();

        public string CheckName(int check)
        {
            (int caseIndex, StreamWindow window) = windows[check];
            return $"case {caseIndex + 1} steps {window.Start}-{window.End - 1} ({window.Target.ToString().ToLowerInvariant()})";
        }

        public string Describe(IReadOnlyList<TrialResult> results)
        {
            IReadOnlyList<float> checks = Checks(results);
            string Right(bool wantsOutput)
            {
                List<int> group = Enumerable.Range(0, windows.Count).Where(check => windows[check].Window.WantsOutput == wantsOutput).ToList();
                return $"{group.Count(check => checks[check] == 1)}/{group.Count}";
            }
            return $"right in {Right(true)} windows that want output and {Right(false)} that want silence";
        }
    }
}
