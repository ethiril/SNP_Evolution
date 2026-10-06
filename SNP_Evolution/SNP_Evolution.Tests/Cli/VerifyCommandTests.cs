using SnpEvolution.Cli;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Storage;
using SnpEvolution.Tests.Evolution;

namespace SnpEvolution.Tests.Cli
{
    public class VerifyCommandTests
    {
        private static string Repository => Path.GetDirectoryName(Settings.DefaultPartLibraryFolder())!;

        private static string Folder() => Path.Combine(Path.GetTempPath(), "snp-verify-" + Guid.NewGuid().ToString("N"));

        [Fact]
        public void VerifyRecordsTheProvenBoundInEachPartFile()
        {
            string folder = Folder();
            Directory.CreateDirectory(folder);
            try
            {
                File.Copy(Path.Combine(Repository, "parts", "delay-2.json"), Path.Combine(folder, "delay-2.json"));

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
        public void VerifyExitsWithTwoAndRecordsTheCounterexample()
        {
            string folder = Folder();
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "register.json");
            try
            {
                Part broken = BoundedCheckTests.RegisterFailingAtTwenty();
                File.WriteAllText(file, PartLibraryFiles.ToJson(LibraryPart.Of(broken, PartEvolution.Measure(broken), new PartOrigin(0, "by hand", 0))));

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
