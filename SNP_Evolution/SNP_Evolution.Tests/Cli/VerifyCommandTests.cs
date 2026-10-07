using SnpEvolution.Cli;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Cli
{
    [Collection(ProcessStateCollection.Name)]
    public class VerifyCommandTests
    {
        [Fact]
        [Slow]
        public void VerifyRecordsTheProvenBoundInEachPartFile()
        {
            using var console = new ConsoleCapture();
            using var temp = new TempFolder("snp-verify").Made();
            string folder = temp.Path;
            File.Copy(RepositoryFiles.PartFile("parts", "delay-2.json"), Path.Combine(folder, "delay-2.json"));

            int exit = CommandLine.Run(new[] { "verify", "--library", folder, "--seconds", "10" });

            Assert.Equal(0, exit);
            ProvenBound? proven = PartLibraryFiles.Load(folder).Parts.Single().Part!.Proven;
            Assert.True(proven?.AllInputs);
        }

        [Fact]
        [Slow]
        public void OnlyVerifiesThePartsWhoseContractNamesMatch()
        {
            using var console = new ConsoleCapture();
            using var temp = new TempFolder("snp-verify").Made();
            string folder = temp.Path;
            foreach (string file in new[] { "delay-2.json", "sequencer-2.json" })
            {
                LibraryPart unproven = RepositoryFiles.ReadPart("parts", file) with { Proven = null };
                File.WriteAllText(Path.Combine(folder, file), PartLibraryFiles.ToJson(unproven));
            }

            int exit = CommandLine.Run(new[] { "verify", "--library", folder, "--only", "DELAY", "--seconds", "10" });

            Assert.Equal(0, exit);
            Dictionary<string, ProvenBound?> proven = PartLibraryFiles.Load(folder).Parts.Select(module => module.Part!).ToDictionary(part => part.Contract.Name, part => part.Proven);
            Assert.NotNull(proven["delay 2"]);
            Assert.Null(proven["sequencer 2"]);
        }

        [Fact]
        public void VerifyExitsWithTwoAndRecordsTheCounterexample()
        {
            using var console = new ConsoleCapture();
            using var temp = new TempFolder("snp-verify").Made();
            string file = Path.Combine(temp.Path, "register.json");
            Part broken = PartFixtures.RegisterFailingAtTwenty();
            File.WriteAllText(file, PartLibraryFiles.ToJson(PartFixtures.Measured(broken, new PartOrigin(0, "by hand", 0))));

            int exit = CommandLine.Run(new[] { "verify", "--part", file, "--bound", "24" });

            Assert.Equal((int)ExitCode.Refuted, exit);
            Assert.Contains("\"FailsAt\": \"n=20\"", File.ReadAllText(file));
        }
    }
}
