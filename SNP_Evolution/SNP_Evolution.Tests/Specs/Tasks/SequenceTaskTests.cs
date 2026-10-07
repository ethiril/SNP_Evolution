using SnpEvolution.Search.Fitness;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;
using static SnpEvolution.Tests.Fixtures.ModuleFixtures;
using static SnpEvolution.Tests.Fixtures.TestNetworks;
using static SnpEvolution.Tests.Fixtures.TrialResults;

namespace SnpEvolution.Tests.Specs.Tasks
{
    public class SequenceTaskTests
    {
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
        public void SequenceGivesPartialCreditForCloseGaps()
        {
            var task = new SequenceTask("twos", new[] { 2, 2 });

            Assert.Equal(0.625f, task.Score(new[] { Trains(new[] { 0, 2, 5 }) }), precision: 5);
        }

        [Fact]
        public void RunsAreLengthenedToFitTheWholeTarget()
        {
            FitnessResult result = Runs.Evaluate(new SequenceTask("twos", Enumerable.Repeat(2, 20).ToList()), PingPong(), new ParallelCpuEngine(), new SimulationOptions(5, 5, OutputTiming.Interval), solvedRetestCount: 3);

            Assert.Equal(1f, result.Fitness);
            Assert.StartsWith("intervals [2,2,2", result.Description);
        }

        [Fact]
        public void SequenceChecksScoreEachGapInPlaceEvenAfterAMistake()
        {
            var task = new SequenceTask("test", new[] { 2, 1, 2 });
            var evaluator = new FitnessEvaluator(new SequentialCpuEngine(), task, new SimulationOptions(20, 2, OutputTiming.Interval), 1, new Random(1), new EvaluationBudget());

            FitnessResult result = evaluator.Evaluate(PingPong());

            Assert.Equal(new[] { 1f, 0f, 1f }, result.Checks);
            Assert.Equal("gap 2 (1)", task.CheckName(1));
            Assert.Equal(new[] { 2, 1, 2 }, ((SequenceTask)task.Focus(1)!).Expected);
        }

        [Fact]
        public void ATriggeredPartCountsItsGapsFromTheTrigger()
        {
            var task = (TriggeredSequenceTask)new SequenceTask("test", new[] { 1, 2, 2, 2, 7 }).Triggered(1)!;
            var evaluator = new FitnessEvaluator(new SequentialCpuEngine(), task, new SimulationOptions(30, 2, OutputTiming.Interval), 1, new Random(1), new EvaluationBudget());

            FitnessResult result = evaluator.Evaluate(TriggeredTwos());
            Cut cut = ModuleCuts.Whole(TriggeredTwos());

            Assert.Equal(new[] { 2, 2, 2 }, task.Expected);
            Assert.Equal(1, task.InputCount);
            Assert.Equal(new[] { 1f, 1f, 1f }, result.Checks);
            Assert.True(Solved.Solves(result.Fitness));
            Assert.Equal(3, cut.Body.Neurons.Count);
            Assert.Equal(new[] { 0 }, cut.Inputs);
        }
    }
}
