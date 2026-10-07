using SnpEvolution.Cli;
using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Verification;
using SnpEvolution.Storage;
using SnpEvolution.Tests.Evolution;

namespace SnpEvolution.Tests.Cli
{
    public class VerifyCommandTests
    {
        private static string Folder() => Path.Combine(Path.GetTempPath(), "snp-verify-" + Guid.NewGuid().ToString("N"));

        [Fact]
        [Slow]
        public void VerifyRecordsTheProvenBoundInEachPartFile()
        {
            string folder = Folder();
            Directory.CreateDirectory(folder);
            try
            {
                File.Copy(RepositoryFiles.PartFile("parts", "delay-2.json"), Path.Combine(folder, "delay-2.json"));

                int exit = CommandLine.Run(new[] { "verify", "--library", folder, "--seconds", "10" });

                Assert.Equal(0, exit);
                ProvenBound? proven = PartLibraryFiles.Load(folder).Parts.Single().Part!.Proven;
                Assert.True(proven?.AllInputs);
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        [Fact]
        [Slow]
        public void OnlyVerifiesThePartsWhoseContractNamesMatch()
        {
            string folder = Folder();
            Directory.CreateDirectory(folder);
            try
            {
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
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        [Fact]
        public void VerifyExitsWithTwoAndRecordsTheCounterexample()
        {
            string folder = Folder();
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "register.json");
            try
            {
                Part broken = BoundedCheckTests.RegisterFailingAtTwenty();
                File.WriteAllText(file, PartLibraryFiles.ToJson(Verifier.Measure(broken, new EvaluationBudget()).ToLibraryPart(broken, new PartOrigin(0, "by hand", 0))));

                int exit = CommandLine.Run(new[] { "verify", "--part", file, "--bound", "24" });

                Assert.Equal(VerifyCommand.Refuted, exit);
                Assert.Contains("\"FailsAt\": \"n=20\"", File.ReadAllText(file));
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
