using SnpEvolution.Evolution;

namespace SnpEvolution.Tests.Evolution
{
    public class FitnessFunctionTests
    {
        private static readonly int[] ExpectedSet = { 1, 2, 3 };

        private static float SetCoverage(params int[] outputs) => new SetCoverageFitness(ExpectedSet).Score(outputs);

        private static float Jaccard(params int[] outputs) => new JaccardFitness(ExpectedSet).Score(outputs);

        [Fact]
        public void NoOutputsScoresZero()
        {
            Assert.Equal(0f, SetCoverage());
            Assert.Equal(0f, Jaccard());
        }

        [Fact]
        public void OutputsCoveringOnlyTheExpectedSetScoreOne()
        {
            Assert.Equal(1f, SetCoverage(1, 2, 3, 1, 2, 3));
            Assert.Equal(1f, Jaccard(1, 2, 3, 1, 2, 3));
        }

        [Fact]
        public void OutputsOutsideTheExpectedSetLowerTheScore()
        {
            Assert.Equal(2f / 3f, SetCoverage(1, 2, 3, 4, 5, 6), precision: 5);
            Assert.Equal(3f / 6f, Jaccard(1, 2, 3, 4, 5, 6), precision: 5);
        }

        [Fact]
        public void SetCoverageScoresASingleHitAmongAsManyOutputsAsExpectedAsZeroRatherThanNaN()
        {
            Assert.Equal(0f, SetCoverage(1, 4, 5));
        }

        [Fact]
        public void PartialCoverageScalesTheScoreDown()
        {
            Assert.Equal(10f / 12f / 3f, SetCoverage(1, 1, 1, 1, 1, 1), precision: 5);
            Assert.Equal(1f / 3f, Jaccard(1, 1, 1, 1, 1, 1), precision: 5);
        }
    }
}
