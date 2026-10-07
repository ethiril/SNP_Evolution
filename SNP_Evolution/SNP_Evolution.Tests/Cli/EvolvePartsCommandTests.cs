using SnpEvolution.Cli;
using SnpEvolution.Model;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Cli
{
    [Collection(ProcessStateCollection.Name)]
    public class EvolvePartsCommandTests
    {
        [Fact]
        [Slow]
        public void EvolvePartsUnderTheProfileSavesAProfilePart()
        {
            using var console = new ConsoleCapture();
            using var temp = new TempFolder("snp-export");
            string folder = temp.Path;

            int exit = CommandLine.Run(new[] { "evolve-parts", "--only", "delay 1", "--budget", "5000", "--profile", "hardware", "--library", folder });

            Assert.Equal((int)ExitCode.Success, exit);
            var part = PartLibraryFiles.Read(File.ReadAllText(Path.Combine(folder, "delay-1.json")), "delay-1.json");
            Assert.Empty(HardwareProfile.Problems(part.Part.Network));
            Assert.EndsWith("--profile hardware", part.Origin.Run);
        }
    }
}
