using SnpEvolution.Cli;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Simulation;
using static SnpEvolution.Tests.TestNetworks;

namespace SnpEvolution.Tests.Evolution
{
    public class OutputTargetTests
    {
        private static TrialResult Trains(params int[][] trains) => new TrialResult(Array.Empty<int>(), false, TrialCoverage.Sampled, trains);

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
            // Half the runs right and half silent: the mean is a half and the worst run nothing, so a quarter.
            Assert.Equal(0.25f, task.Score(new[] { Trains(new[] { 0, 1, 2, 4, 7 }, Array.Empty<int>()) }));
        }

        [Fact]
        public void SequenceScoreStopsAtTheFirstWrongGap()
        {
            var task = new SequenceTask("fib", new[] { 1, 1, 2, 3 });

            // Gaps 1,2,2,3: one right, then 2 for 1 earns a quarter, and the 2,3 after the mistake earn nothing.
            Assert.Equal(0.3125f, task.Score(new[] { Trains(new[] { 0, 1, 3, 5, 8 }) }), precision: 5);
        }

        [Fact]
        public void SequenceNicheIsTheRightPrefixByTheLongestGap()
        {
            var task = new SequenceTask("fib", new[] { 1, 1, 2, 3, 5 });

            Assert.Equal((3, 3), task.Niche(new[] { Trains(new[] { 0, 1, 2, 4, 9 }) }));
            Assert.Equal((0, 0), task.Niche(new[] { Trains(new[] { 4 }) }));
        }

        [Fact]
        public void PrefixesKeepTheOpeningValues()
        {
            var sequence = new SequenceTask("fib", new[] { 1, 1, 2, 3, 5 });
            var word = new SpikeWordTask("w", new[] { true, false, true, true });

            Assert.Equal(new[] { 1, 1, 2 }, ((SequenceTask)sequence.Prefix(3)).Expected);
            Assert.Equal(new[] { true, false }, ((SpikeWordTask)word.Prefix(2)).Expected);
            Assert.True(sequence.Prefix(3).StepsNeeded < sequence.StepsNeeded);
        }

        [Fact]
        public void SpikeWordNicheIsTheRightPrefixBySpikeCount()
        {
            var task = new SpikeWordTask("w", new[] { true, false, true, true });

            // Fires on steps 0 and 2: right for three steps, with two spikes, as shares of 16 and 8 buckets.
            Assert.Equal((12, 4), task.Niche(new[] { Trains(new[] { 0, 2 }) }));
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
