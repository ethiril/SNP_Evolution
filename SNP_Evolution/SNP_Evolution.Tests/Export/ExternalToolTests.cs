using SnpEvolution.Export;

namespace SnpEvolution.Tests.Export
{
    public class ExternalToolTests
    {
        // The co-simulation tests skip when a tool is not found, so a broken lookup would pass silently without this.
        [Fact]
        public void FindsAProgramOnThePathAndRunsIt()
        {
            string shell = Assert.IsType<string>(ExternalTool.Find("sh"));

            Assert.Equal("found\n", ExternalTool.Run(shell, "-c \"echo found\"", Path.GetTempPath()));
            Assert.Null(ExternalTool.Find("no-such-program-anywhere"));
        }
    }
}
