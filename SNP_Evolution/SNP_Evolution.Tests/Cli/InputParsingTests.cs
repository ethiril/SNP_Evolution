using SnpEvolution.Cli;

namespace SnpEvolution.Tests.Cli
{
    public class InputParsingTests
    {
        [Theory]
        [InlineData("12", true)]
        [InlineData("0", false)]
        [InlineData("-3", false)]
        [InlineData("abc", false)]
        public void PositiveIntRejectsZeroNegativesAndText(string input, bool accepted)
        {
            Assert.Equal(accepted, InputParsing.TryPositiveInt(input, out _));
        }

        [Theory]
        [InlineData("0.25", true)]
        [InlineData("1", true)]
        [InlineData("1.5", false)]
        [InlineData("-0.1", false)]
        public void ProbabilityMustLieBetweenZeroAndOne(string input, bool accepted)
        {
            Assert.Equal(accepted, InputParsing.TryProbability(input, out _));
        }

        [Fact]
        public void IntegerSetToleratesSpacesAfterCommas()
        {
            Assert.True(InputParsing.TryIntegerSet("1, 2,3", out List<int> values));
            Assert.Equal(new[] { 1, 2, 3 }, values);
        }

        [Fact]
        public void IntegerSetRejectsAnyNonInteger()
        {
            Assert.False(InputParsing.TryIntegerSet("1,x,3", out _));
        }
    }
}
