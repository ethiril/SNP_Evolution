using SnpEvolution.Cli;

namespace SnpEvolution.Tests.Cli
{
    public class ExportCommandsTests
    {
        private static string Folder() => Path.Combine(Path.GetTempPath(), "snp-export-" + Guid.NewGuid().ToString("N"));

        [Fact]
        public void ExportVerilogWritesTheDesignTestbenchAndExpectedTrace()
        {
            string folder = Folder();
            try
            {
                int exit = CommandLine.Run(new[] { "export-verilog", "--part", RepositoryFiles.PartFile("parts", "delay-2.json"), "--out", folder, "--check", "off" });

                Assert.Equal(0, exit);
                Assert.All(new[] { "delay_2.v", "delay_2_tb.v", "delay_2_expected.txt" }, file => Assert.True(File.Exists(Path.Combine(folder, file)), file));
            }
            finally
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                }
            }
        }

        [Fact]
        public void ExportUppaalWritesTheModelAndItsQueries()
        {
            string folder = Folder();
            try
            {
                int exit = CommandLine.Run(new[] { "export-uppaal", "--part", RepositoryFiles.PartFile("parts", "delay-2.json"), "--out", folder, "--check", "off" });

                Assert.Equal(0, exit);
                Assert.All(new[] { "delay-2.xml", "delay-2.q" }, file => Assert.True(File.Exists(Path.Combine(folder, file)), file));
            }
            finally
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                }
            }
        }

        [Fact]
        public void ExportNirRefusesAPartOutsideTheProfile()
        {
            string folder = Folder();

            int exit = CommandLine.Run(new[] { "export-nir", "--part", RepositoryFiles.PartFile("parts", "delay-2.json"), "--out", folder });

            Assert.Equal(1, exit);
            Assert.False(Directory.Exists(folder));
        }

        [Fact]
        [Slow]
        public void EvolvePartsUnderTheProfileSavesAProfilePart()
        {
            string folder = Folder();
            try
            {
                int exit = CommandLine.Run(new[] { "evolve-parts", "--only", "delay 1", "--budget", "5000", "--profile", "hardware", "--library", folder });

                Assert.Equal(0, exit);
                var part = SnpEvolution.Storage.PartLibraryFiles.Read(File.ReadAllText(Path.Combine(folder, "delay-1.json")), "delay-1.json");
                Assert.Empty(SnpEvolution.Evolution.HardwareProfile.Problems(part.Part.Network));
                Assert.EndsWith("--profile hardware", part.Origin.Run);
            }
            finally
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                }
            }
        }

        [Fact]
        public void ExportNeedsAPartOrANetwork()
        {
            Assert.Equal(1, CommandLine.Run(new[] { "export-verilog", "--out", Folder() }));
        }
    }
}
