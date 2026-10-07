using SnpEvolution.Model;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Tasks;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Specs.Tasks
{
    public class StreamingTaskTests
    {
        private static readonly StreamingTask Debouncer = StreamingTask.Debouncer(spikes: 2, within: 3);
        private static readonly StreamingTask RateDetector = StreamingTask.RateDetector(spikes: 3, within: 6);

        // The input and two relays bring each input spike to the counter on three steps running, so it holds the input's last three steps, forgets one spike and fires on two or three.
        private static Neuron[] WindowOfThree(int counterTargets) => new[]
        {
            InputNeuron(new[] { 2, 3 }, Standard("a", 1)),
            Neuron(0, new[] { counterTargets }, StandardForget("a", 1), Standard("aa", 2), Standard("aaa", 3)),
            Neuron(0, new[] { 2, 4 }, Standard("a", 1)),
            Neuron(0, new[] { 2 }, Standard("a", 1)),
        };

        // The output closes for two steps after it takes a spike, so the rest of a burst is lost on it: one answer per burst.
        private static Network HandBuiltDebouncer() => new Network(WindowOfThree(5).Append(OutputNeuron(0, Standard("a", 1, delay: 2))).ToArray());

        private static Network HandBuiltRateDetector() => new Network(WindowOfThree(5).Append(OutputNeuron(0, Standard("a", 1))).ToArray());

        private static FitnessResult Evaluate(ITask task, Network network) =>
            Runs.Evaluate(task, network, new SequentialCpuEngine(), new SimulationOptions(0, 5, OutputTiming.Interval), solvedRetestCount: 3);

        [Fact]
        public void InputsAreTheSameEveryTime()
        {
            StreamingTask again = StreamingTask.Debouncer(spikes: 2, within: 3);

            Assert.Equal(Debouncer.Streams.Select(stream => stream.InputSteps), again.Streams.Select(stream => stream.InputSteps));
            Assert.NotEqual(Debouncer.Streams[0].InputSteps, Debouncer.Streams[1].InputSteps);
        }

        [Fact]
        public void EveryDebouncerCaseHasBurstsAndGlitches()
        {
            List<StreamWindow> windows = Debouncer.Streams.SelectMany(stream => stream.Windows).ToList();

            Assert.Contains(windows, window => window.Target == WindowTarget.Once);
            Assert.Contains(windows, window => window.Target == WindowTarget.Silent);
            Assert.Equal(windows.Count, Debouncer.Checks(Debouncer.Cases.Select(_ => new TrialResult(Array.Empty<int>(), false, TrialCoverage.Sampled)).ToList()).Count);
        }

        [Fact]
        public void HandBuiltDebouncerSolvesItsTask()
        {
            FitnessResult result = Evaluate(Debouncer, HandBuiltDebouncer());

            Assert.Equal(1f, result.Fitness);
            Assert.All(result.Checks!, check => Assert.Equal(1f, check));
        }

        [Fact]
        public void CopyingTheInputScoresBelowAHalf()
        {
            Assert.InRange(Evaluate(Debouncer, Identity()).Fitness, 0f, 0.49f);
        }

        [Fact]
        public void SilenceScoresAHalf()
        {
            Assert.Equal(0.5f, Evaluate(Debouncer, NeverOutputs()).Fitness);
            Assert.Equal(0.5f, Evaluate(RateDetector, NeverOutputs()).Fitness);
        }

        [Fact]
        public void DebouncerWithoutRefractoryAnswersSomeBurstsTwice()
        {
            float fitness = Evaluate(Debouncer, HandBuiltRateDetector()).Fitness;

            Assert.InRange(fitness, 0.5f, 0.99f);
        }

        [Fact]
        public void HandBuiltRateDetectorSolvesItsTask()
        {
            Assert.Equal(1f, Evaluate(RateDetector, HandBuiltRateDetector()).Fitness);
        }

        [Fact]
        public void WindowsJudgeCountsOfOutputSpikes()
        {
            Assert.True(new StreamWindow(2, 5, WindowTarget.Once).Passes(new[] { 1, 3, 5 }));
            Assert.False(new StreamWindow(2, 5, WindowTarget.Once).Passes(new[] { 2, 4 }));
            Assert.True(new StreamWindow(2, 5, WindowTarget.Active).Passes(new[] { 2, 4 }));
            Assert.False(new StreamWindow(2, 5, WindowTarget.Silent).Passes(new[] { 4 }));
            Assert.True(new StreamWindow(2, 5, WindowTarget.Silent).Passes(new[] { 1, 5 }));
        }
    }
}
