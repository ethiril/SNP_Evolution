using SnpEvolution.Application;

namespace SnpEvolution.Tests.Application
{
    public class LibraryViewTests
    {
        private static PartFile Kept(string file) => new PartFile(RepositoryFiles.ReadPart("parts", file), Path.Combine("parts", file));

        [Fact]
        public void TheTableListsEachPartAndTheContractsStillWithoutOne()
        {
            string table = LibraryView.Table(new[] { Kept("sequencer-2.json"), Kept("delay-2.json") });

            string[] lines = table.Split(Environment.NewLine);
            Assert.StartsWith("delay 2", lines[1]);
            Assert.StartsWith("sequencer 2", lines[2]);
            Assert.Contains("all inputs", lines[1]);
            Assert.Contains("No part yet for: delay 1, delay 3, delay 4, fan-out", table);
        }

        [Fact]
        public void APartInFullShowsItsGoalPortsReadingsAndNetwork()
        {
            string text = LibraryView.Describe(Kept("sequencer-2.json"));

            Assert.Contains("Goal: fires its outputs in order", text);
            Assert.Contains("t1 (out, trigger) on neuron", text);
            Assert.Contains("  case 1: done,t1=1,t2=1", text);
            Assert.Contains("Neuron", text);
            Assert.Contains("Sends to", text);
        }
    }
}
