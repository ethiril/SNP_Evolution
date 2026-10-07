using SnpEvolution.Cli;

namespace SnpEvolution.Tests.Cli
{
    [Collection(ProcessStateCollection.Name)]
    public class PartsCommandTests
    {
        private static CommandRun WithParts()
        {
            var run = new CommandRun();
            run.CopyFile(Path.Combine("parts", "delay-2.json"));
            run.CopyFile(Path.Combine("parts", "sequencer-2.json"));
            return run;
        }

        [Fact]
        public void WithoutOptionsItListsTheKeptParts()
        {
            using CommandRun run = WithParts();

            string output = run.Run("parts");

            Assert.Contains("exit 0", output);
            Assert.Contains("delay 2", output);
            Assert.Contains("sequencer 2", output);
            Assert.Contains("No part yet for:", output);
        }

        [Fact]
        public void OnlyShowsTheMatchingPartsInFull()
        {
            using CommandRun run = WithParts();

            string output = run.Run("parts", "--only", "sequencer");

            Assert.Contains("exit 0", output);
            Assert.Contains("What it reads on each case:", output);
            Assert.DoesNotContain("delay 2", output.Split("--- stderr")[0]);
        }

        [Fact]
        public void APartNoneMatchesIsRefusedNamingWhatTheLibraryHas()
        {
            using CommandRun run = WithParts();

            string output = run.Run("parts", "--only", "zero");

            Assert.Contains($"exit {(int)ExitCode.Usage}", output);
            Assert.Contains("The library has: ", output);
        }
    }
}
