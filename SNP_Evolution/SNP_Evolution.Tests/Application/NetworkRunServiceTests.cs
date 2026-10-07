using SnpEvolution.Application;

namespace SnpEvolution.Tests.Application
{
    public class NetworkRunServiceTests
    {
        private static Settings SingleThread() => new Settings { Engine = Catalog.Engines.Single(engine => engine.Name == "CPU, single thread") };

        [Fact]
        public void ANetworkWithAnInputIsScoredOnTheSelectedTask()
        {
            Settings settings = SingleThread();

            var run = Assert.IsType<NetworkRun.Scored>(NetworkRunService.Run(settings, TestNetworks.Identity(), new Random(1)));

            Assert.Equal(settings.SelectedTask.Name, run.Task.Name);
        }

        [Fact]
        public void ANetworkWithoutInputsReportsTheStepsItSpikedAt()
        {
            var run = Assert.IsType<NetworkRun.SpikeTrain>(NetworkRunService.Run(SingleThread(), TestNetworks.PingPong(), new Random(1)));

            Assert.Equal(new[] { 1, 3, 5 }, run.SpikeSteps.Take(3));
        }
    }
}
