using SnpEvolution.Application;
using SnpEvolution.Cli;

namespace SnpEvolution.Tests.Cli
{
    [Collection(ProcessStateCollection.Name)]
    public class ExportCommandsTests
    {
        private static readonly string Delay2 = RepositoryFiles.PartFile("parts", "delay-2.json");

        [Fact]
        public void ExportVerilogWritesTheDesignTestbenchAndExpectedTrace()
        {
            using var console = new ConsoleCapture();
            using var temp = new TempFolder("snp-export");
            string folder = temp.Path;

            int exit = CommandLine.Run(new[] { "export-verilog", "--part", Delay2, "--out", folder, "--check", "off" });

            Assert.Equal((int)ExitCode.Success, exit);
            Assert.All(new[] { "delay_2.v", "delay_2_tb.v", "delay_2_expected.txt" }, file => Assert.True(File.Exists(Path.Combine(folder, file)), file));
        }

        [Fact]
        public void ExportUppaalWritesTheModelAndItsQueries()
        {
            using var console = new ConsoleCapture();
            using var temp = new TempFolder("snp-export");
            string folder = temp.Path;

            int exit = CommandLine.Run(new[] { "export-uppaal", "--part", Delay2, "--out", folder, "--check", "off" });

            Assert.Equal((int)ExitCode.Success, exit);
            Assert.All(new[] { "delay-2.xml", "delay-2.q" }, file => Assert.True(File.Exists(Path.Combine(folder, file)), file));
        }

        [Fact]
        public void ExportNirRefusesAPartOutsideTheProfile()
        {
            using var console = new ConsoleCapture();
            using var temp = new TempFolder("snp-export");
            string folder = temp.Path;

            int exit = CommandLine.Run(new[] { "export-nir", "--part", Delay2, "--out", folder });

            Assert.Equal((int)ExitCode.Usage, exit);
            Assert.False(Directory.Exists(folder));
        }

        [Fact]
        public void ACheckThatDiffersFromOurEngineHasItsOwnExitCode()
        {
            Assert.Equal(ExitCode.Success, ExportCommand.ExitFor(ExportStatus.Written));
            Assert.Equal(ExitCode.Usage, ExportCommand.ExitFor(ExportStatus.Failed));
            Assert.Equal(ExitCode.Differs, ExportCommand.ExitFor(ExportStatus.Differs));
        }

        [Fact]
        public void ExportNeedsAPartOrANetwork()
        {
            using var console = new ConsoleCapture();
            using var temp = new TempFolder("snp-export");

            Assert.Equal((int)ExitCode.Usage, CommandLine.Run(new[] { "export-verilog", "--out", temp.Path }));
        }
    }
}
