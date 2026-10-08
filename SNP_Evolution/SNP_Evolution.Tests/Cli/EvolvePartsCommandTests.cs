using SnpEvolution.Cli;
using SnpEvolution.Model;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Cli
{
    [Collection(ProcessStateCollection.Name)]
    public class EvolvePartsCommandTests
    {
        // Compiling only under the profile refuses every contract at once, so the log names exactly the contracts picked.
        [Theory]
        [InlineData("add", "add")]
        [InlineData("decrement", "decrement")]
        [InlineData("delay", "delay 1,delay 2,delay 3,delay 4")]
        public void AContractsOwnNamePicksOnlyItAndAnyOtherNamePicksEveryMatch(string only, string picked)
        {
            using var console = new ConsoleCapture();
            using var temp = new TempFolder("snp-only");

            CommandLine.Run(new[] { "evolve-parts", "--only", only, "--route", "compile", "--profile", "hardware", "--library", temp.Path });

            IEnumerable<string> refused = console.Printed.Split(Environment.NewLine).Where(line => line.Contains(": not compiled")).Select(line => line[..line.IndexOf(':')]);
            Assert.Equal(picked.Split(','), refused.Order());
        }

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
