using SnpEvolution.Specs.Accounting;

namespace SnpEvolution.Tests.Specs.Accounting
{
    public class StatisticsTests
    {
        // The two-sample example in R's wilcox.test help: W = 58, and the normal approximation gives a two-sided p of
        // about 0.133 (z = 17 / sqrt(128.25)), close to the exact value.
        [Fact]
        public void MatchesRsWilcoxonExample()
        {
            double[] x = { 1.83, 0.50, 1.62, 2.48, 1.68, 1.88, 1.55, 3.06, 1.30 };
            double[] y = { 0.878, 0.647, 0.598, 2.05, 1.06, 1.29, 1.06, 3.14, 1.29 };

            MannWhitneyResult result = Statistics.MannWhitney(x, y);

            Assert.True(result.Exact);
            Assert.Equal(58, result.U);
            Assert.Equal(58 / 81.0, result.A12, 6);
            Assert.InRange(result.P, 0.125, 0.135);
        }

        [Fact]
        public void SeparatedSamplesGiveTheSmallestExactP()
        {
            MannWhitneyResult result = Statistics.MannWhitney(new double[] { 4, 5, 6 }, new double[] { 1, 2, 3 });

            Assert.Equal(9, result.U);
            Assert.Equal(1, result.A12);
            Assert.Equal(0.1, result.P, 9);
        }

        [Fact]
        public void TiedSamplesShowNoDifference()
        {
            MannWhitneyResult result = Statistics.MannWhitney(new double[] { 5, 5, 5, 5 }, new double[] { 5, 5, 5, 5 });

            Assert.Equal(0.5, result.A12);
            Assert.Equal(1, result.P, 9);
        }

        [Fact]
        public void LargeSamplesUseTheNormalApproximation()
        {
            double[] first = Enumerable.Range(0, 30).Select(value => (double)value).ToArray();
            double[] second = Enumerable.Range(10, 30).Select(value => (double)value).ToArray();

            MannWhitneyResult result = Statistics.MannWhitney(first, second);

            Assert.False(result.Exact);
            // 2.245e-4 with the continuity correction; without it the p-value would be 2.118e-4.
            Assert.InRange(result.P, 2.22e-4, 2.27e-4);
            Assert.True(result.A12 < 0.5);
        }
    }
}
