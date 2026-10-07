using SnpEvolution.Application;

namespace SnpEvolution.Tests.Application
{
    public class TargetPatternsTests
    {
        [Fact]
        public void FindsTheTypoInAFibonacciTarget()
        {
            SuspectedTypo? typo = TargetPatterns.Find(Sequences.FibonacciWithTypo.Take(16).ToArray());

            Assert.NotNull(typo);
            Assert.Equal((11, 114, 144), (typo!.Index, typo.Found, typo.Expected));
        }

        [Theory]
        [InlineData(new[] { 2, 4, 6, 9, 10 }, 3, 8)]
        [InlineData(new[] { 1, 2, 4, 8, 15, 32 }, 4, 16)]
        [InlineData(new[] { 7, 1, 2, 3, 5, 8 }, 0, 1)]
        public void FindsASingleValueThatBreaksAPattern(int[] values, int index, int expected)
        {
            SuspectedTypo? typo = TargetPatterns.Find(values);

            Assert.Equal((index, expected), (typo!.Index, typo.Expected));
        }

        [Theory]
        [InlineData(new[] { 1, 1, 2, 3, 5, 8, 13 })]
        [InlineData(new[] { 3, 1, 4, 1, 5, 9 })]
        [InlineData(new[] { 1, 1, 2, 4 })]
        public void LeavesTargetsWithoutAClearTypoAlone(int[] values)
        {
            Assert.Null(TargetPatterns.Find(values));
        }
    }
}
