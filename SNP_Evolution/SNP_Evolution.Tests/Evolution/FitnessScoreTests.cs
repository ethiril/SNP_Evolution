using SnpEvolution.Evolution;

namespace SnpEvolution.Tests.Evolution
{
    public class FitnessScoreTests
    {
        private static readonly int[] ExpectedSet = { 1, 2, 3 };

        [Fact]
        public void NoOutputsScoresZero()
        {
            Assert.Equal(0f, FitnessScore.Calculate(Array.Empty<int>(), ExpectedSet));
        }

        [Fact]
        public void OutputsCoveringOnlyTheExpectedSetScoreOne()
        {
            Assert.Equal(1f, FitnessScore.Calculate(new[] { 1, 2, 3, 1, 2, 3 }, ExpectedSet));
        }

        [Fact]
        public void OutputsOutsideTheExpectedSetLowerTheScore()
        {
            Assert.Equal(2f / 3f, FitnessScore.Calculate(new[] { 1, 2, 3, 4, 5, 6 }, ExpectedSet), precision: 5);
        }

        [Fact]
        public void SingleHitAmongAsManyOutputsAsExpectedScoresZeroRatherThanNaN()
        {
            Assert.Equal(0f, FitnessScore.Calculate(new[] { 1, 4, 5 }, ExpectedSet));
        }

        [Fact]
        public void PartialCoverageScalesTheScoreDown()
        {
            Assert.Equal(10f / 12f / 3f, FitnessScore.Calculate(new[] { 1, 1, 1, 1, 1, 1 }, ExpectedSet), precision: 5);
        }
    }
}
