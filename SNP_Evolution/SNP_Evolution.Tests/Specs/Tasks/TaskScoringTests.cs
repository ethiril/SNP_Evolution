using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Tests.Specs.Tasks
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

    }
}
