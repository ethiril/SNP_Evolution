using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Cli
{
    [Collection(ProcessStateCollection.Name)]
    public class RunCommandTests
    {
        [Fact]
        public void AReferenceNetworkShowsItsSpikeTrain()
        {
            using var run = new CommandRun();

            string output = run.Run("run", "--network", "even", "--steps", "20", "--repetitions", "3", "--seed", "1");

            Assert.Contains("exit 0", output);
            Assert.Contains("Intervals between its spikes: ", output);
        }

        [Fact]
        public void ANetworkWithInputsIsScoredOnTheTask()
        {
            using var run = new CommandRun();
            NetworkFiles.Save(TestNetworks.Identity(), Path.Combine(run.Folder, "identity.json"));

            string output = run.Run("run", "--network", "identity.json", "--task", "Compute n", "--seed", "1");

            Assert.Contains("exit 0", output);
            Assert.Contains("On Compute n: fitness", output);
        }
    }
}
