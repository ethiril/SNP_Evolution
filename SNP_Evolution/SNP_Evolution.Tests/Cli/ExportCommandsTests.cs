using SnpEvolution.Application;
using SnpEvolution.Cli;

namespace SnpEvolution.Tests.Cli
{
    [Collection(ProcessStateCollection.Name)]
    public class ExportCommandsTests
    {
        [Fact]
        public void ExportVerilogWritesTheDesignTestbenchAndExpectedTrace()
        {
            using var console = new ConsoleCapture();
            using var temp = new TempFolder("snp-export");
            string folder = temp.Path;

            int exit = CommandLine.Run(new[] { "export-verilog", "--part", RepositoryFiles.PartFile("parts", "delay-2.json"), "--out", folder, "--check", "off" });

            Assert.Equal(0, exit);
            Assert.All(new[] { "delay_2.v", "delay_2_tb.v", "delay_2_expected.txt" }, file => Assert.True(File.Exists(Path.Combine(folder, file)), file));
        }

        [Fact]
        public void ExportUppaalWritesTheModelAndItsQueries()
        {
            using var console = new ConsoleCapture();
            using var temp = new TempFolder("snp-export");
            string folder = temp.Path;

            int exit = CommandLine.Run(new[] { "export-uppaal", "--part", RepositoryFiles.PartFile("parts", "delay-2.json"), "--out", folder, "--check", "off" });

            Assert.Equal(0, exit);
            Assert.All(new[] { "delay-2.xml", "delay-2.q" }, file => Assert.True(File.Exists(Path.Combine(folder, file)), file));
        }

        [Fact]
        public void ExportNirRefusesAPartOutsideTheProfile()
        {
            using var console = new ConsoleCapture();
            using var temp = new TempFolder("snp-export");
            string folder = temp.Path;


            int exit = CommandLine.Run(new[] { "export-nir", "--part", RepositoryFiles.PartFile("parts", "delay-2.json"), "--out", folder });

            Assert.Equal(1, exit);
            Assert.False(Directory.Exists(folder));
        }

        [Fact]
        [Slow]
        public void EvolvePartsUnderTheProfileSavesAProfilePart()
        {
            using var console = new ConsoleCapture();
            using var temp = new TempFolder("snp-export");
            string folder = temp.Path;

            int exit = CommandLine.Run(new[] { "evolve-parts", "--only", "delay 1", "--budget", "5000", "--profile", "hardware", "--library", folder });

            Assert.Equal(0, exit);
            var part = SnpEvolution.Storage.PartLibraryFiles.Read(File.ReadAllText(Path.Combine(folder, "delay-1.json")), "delay-1.json");
            Assert.Empty(SnpEvolution.Model.HardwareProfile.Problems(part.Part.Network));
            Assert.EndsWith("--profile hardware", part.Origin.Run);
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

            Assert.Equal(1, CommandLine.Run(new[] { "export-verilog", "--out", temp.Path }));
        }
    }
}
