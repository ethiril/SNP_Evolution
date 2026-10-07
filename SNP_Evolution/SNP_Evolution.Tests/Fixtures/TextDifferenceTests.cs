namespace SnpEvolution.Tests.Fixtures
{
    public class TextDifferenceTests
    {
        [Fact]
        public void NamesTheFirstLineThatDiffersIncludingAMissingLastLine()
        {
            Assert.Null(TextDifference.FirstDifference("a\nb\n", "a\nb\n"));
            Assert.Equal("First difference at line 2:\n  expected: b\n  actual:   c", TextDifference.FirstDifference("a\nb\n", "a\nc\n"));
            Assert.Equal("First difference at line 3:\n  expected: c\n  actual:   (end of file)", TextDifference.FirstDifference("a\nb\nc", "a\nb"));
        }
    }
}
