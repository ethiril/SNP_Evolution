using SnpEvolution.Application;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Application
{
    public class ExportServiceTests
    {
        [Fact]
        public void AnExportedNetworkRunsForTheStepsGiven()
        {
            using var temp = new TempFolder("snp-network").Made();
            string file = Path.Combine(temp.Path, "network.json");
            NetworkFiles.Save(TestNetworks.PingPong(), file);

            Assert.Equal(7, Assert.Single(ExportService.Network(file, 7).Value!.Cases).Steps);
        }
    }
}
