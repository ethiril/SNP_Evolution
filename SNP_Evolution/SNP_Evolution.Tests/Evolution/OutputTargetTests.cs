using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Simulation;
using static SnpEvolution.Tests.TestNetworks;

namespace SnpEvolution.Tests.Evolution
{
    public class OutputTargetTests
    {
        private static TrialResult Trains(params int[][] trains) => new TrialResult(Array.Empty<int>(), false, false, trains);

        private static FitnessResult Evaluate(ITask task, int maxSteps) =>
            new FitnessEvaluator(new ParallelCpuEngine(), task, new SimulationOptions(maxSteps, 5, OutputTiming.Interval), 3, new Random(0)).Evaluate(PingPong());

        [Theory]
        [InlineData("1,1,2,3,5,8,13", new[] { 1, 1, 2, 3, 5, 8, 13 })]
        [InlineData("1, 1, 2, 3, 5 ...", new[] { 1, 1, 2, 3, 5 })]
        [InlineData("{2 4 6}", new[] { 2, 4, 6 })]
        [InlineData("[3,1,4]…", new[] { 3, 1, 4 })]
        public void NumbersParseWithAnySeparatorsBracketsAndEllipsis(string input, int[] expected)
        {
            Assert.True(OutputTarget.TryParse(TargetKind.Sequence, input, out OutputTarget target));
            Assert.Equal(expected, target.Values);
        }

        [Theory]
        [InlineData("")]
        [InlineData("1,0,2")]
        [InlineData("1,-2")]
        [InlineData("1,x")]
        public void NumbersMustBePositiveIntegers(string input)
        {
            Assert.False(OutputTarget.TryParse(TargetKind.Set, input, out _));
        }

        [Fact]
        public void BinaryWordsIgnoreSpacingAndNeedAOne()
        {
            Assert.True(OutputTarget.TryParse(TargetKind.BinaryWord, "0110 1001_0", out OutputTarget target));
            Assert.Equal(new[] { 0, 1, 1, 0, 1, 0, 0, 1, 0 }, target.Values);
            Assert.False(OutputTarget.TryParse(TargetKind.BinaryWord, "0000", out _));
            Assert.False(OutputTarget.TryParse(TargetKind.BinaryWord, "0120", out _));
        }

        [Fact]
        public void EachKindMakesItsTask()
        {
            var fitness = new JaccardFitness(new[] { 1 });

            Assert.IsType<GeneratorTask>(new OutputTarget(TargetKind.Set, new[] { 1, 1, 2 }).CreateTask(fitness));
            Assert.IsType<SequenceTask>(new OutputTarget(TargetKind.Sequence, new[] { 1, 1, 2 }).CreateTask(fitness));
            Assert.IsType<SpikeWordTask>(new OutputTarget(TargetKind.BinaryWord, new[] { 0, 1 }).CreateTask(fitness));
        }

        [Fact]
        public void SequenceScoreRewardsTheRightGapsInOrder()
        {
            var task = new SequenceTask("fib", new[] { 1, 1, 2, 3 });

            Assert.Equal(1f, task.Score(new[] { Trains(new[] { 4, 5, 6, 8, 11, 20 }) }));
            Assert.Equal(0.5f, task.Score(new[] { Trains(new[] { 0, 1, 2 }) }));
            Assert.Equal(0.5f, task.Score(new[] { Trains(new[] { 0, 1, 2, 4, 7 }, Array.Empty<int>()) }));
        }

        [Fact]
        public void SequenceGivesPartialCreditForCloseGaps()
        {
            var task = new SequenceTask("twos", new[] { 2, 2 });

            Assert.Equal(0.625f, task.Score(new[] { Trains(new[] { 0, 2, 5 }) }), precision: 5);
        }

        [Fact]
        public void BinaryWordScoreIsBalancedAccuracy()
        {
            var task = new SpikeWordTask("w", new[] { false, true, false, true });

            Assert.Equal(1f, task.Score(new[] { Trains(new[] { 1, 3, 9 }) }));
            Assert.Equal(0.5f, task.Score(new[] { Trains(Array.Empty<int>()) }));
            Assert.Equal(0.5f, task.Score(new[] { Trains(new[] { 0, 1, 2, 3 }) }));
        }

        [Fact]
        public void RunsAreLengthenedToFitTheWholeTarget()
        {
            FitnessResult result = Evaluate(new SequenceTask("twos", Enumerable.Repeat(2, 20).ToList()), maxSteps: 5);

            Assert.Equal(1f, result.Fitness);
            Assert.StartsWith("intervals [2,2,2", result.Description);
        }

        [Fact]
        public void PingPongSpellsAlternatingBits()
        {
            FitnessResult result = Evaluate(new SpikeWordTask("01", Enumerable.Range(0, 12).Select(step => step % 2 == 1).ToList()), maxSteps: 3);

            Assert.Equal(1f, result.Fitness);
            Assert.Equal("spikes 010101010101 / 010101010101", result.Description);
        }
    }
}
