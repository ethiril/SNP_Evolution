namespace SnpEvolution.Tests.Golden
{
    public class GoldenFileTests
    {
        [Fact]
        public void NamesTheFirstLineThatDiffersIncludingAMissingLastLine()
        {
            Assert.Null(GoldenFile.FirstDifference("a\nb\n", "a\nb\n"));
            Assert.Equal("First difference at line 2:\n  expected: b\n  actual:   c", GoldenFile.FirstDifference("a\nb\n", "a\nc\n"));
            Assert.Equal("First difference at line 3:\n  expected: c\n  actual:   (end of file)", GoldenFile.FirstDifference("a\nb\nc", "a\nb"));
        }
    }
}
