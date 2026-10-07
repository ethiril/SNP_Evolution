using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Tests.Evolution
{
    public class TaskScoringTests
    {
        [Fact]
        public void BalancedAccuracyAveragesTheTwoGroups()
        {
            // Three of four in the first group right, none of two in the second.
            bool[] inFirst = { true, true, true, true, false, false };
            float[] scores = { 1, 1, 1, 0, 0, 0 };

            Assert.Equal(0.375f, TaskScoring.BalancedAccuracy(Enumerable.Range(0, 6), item => inFirst[item], item => scores[item]));
        }

        [Fact]
        public void BalancedAccuracyOfOneGroupIsItsMeanAndOfNothingIsZero()
        {
            Assert.Equal(0.5f, TaskScoring.BalancedAccuracy(new[] { 1f, 0f }, _ => true, score => score));
            Assert.Equal(0f, TaskScoring.BalancedAccuracy(Array.Empty<float>(), _ => true, score => score));
        }

        [Fact]
        public void TheShareOfRunsIsExact()
        {
            int[] runs = { 1, 1, 1 };

            Assert.Equal(1f, TaskScoring.ShareOfRuns(runs, run => run == 1));
            Assert.Equal(0f, TaskScoring.ShareOfRuns(Array.Empty<int>(), _ => true));
        }

        [Fact]
        public void TheCorrectPrefixStopsAtTheFirstMistakeOrTheShorterList()
        {
            Assert.Equal(2, TaskScoring.CorrectPrefix(new[] { 1, 2, 9, 4 }, new[] { 1, 2, 3, 4 }));
            Assert.Equal(1, TaskScoring.CorrectPrefix(new[] { 1 }, new[] { 1, 2 }));
            Assert.Equal(2, TaskScoring.CorrectPrefix(new[] { 1, 2, 3 }, new[] { 1, 2 }));
        }

        // A number on an input and an interval port's value are the one encoding.
        [Fact]
        public void ANumberIsEncodedAsAnIntervalPortIs()
        {
            Assert.Equal(PortEncoding.Interval(5, 3), InputSpikes.Interval(5, 3));
            Assert.Equal(new[] { 0, 5 }, InputSpikes.Numbers(5).StepsPerInput[0]);
        }

        [Fact]
        public void BinaryWordsStartWithLongerStagesThanSequences()
        {
            Assert.Equal(new CurriculumPlan(8, 4), ((IPrefixTask)new SpikeWordTask("word", Enumerable.Repeat(true, 20).ToList())).Curriculum);
            Assert.Equal(new CurriculumPlan(3, 1), ((IPrefixTask)new SequenceTask("gaps", new[] { 1, 2, 3, 4, 5 })).Curriculum);
        }
    }
}
