using SnpEvolution.Search.Fitness;
using SnpEvolution.Specs.Tasks;
using static SnpEvolution.Tests.Fixtures.ModuleFixtures;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Search.Fitness
{
    public class CheckDiagnosisTests
    {
        [Fact]
        public void DiagnosisFindsTheFirstCheckNoNetworkDoes()
        {
            CheckDiagnosis diagnosis = CheckDiagnosis.Of(new[] { Scored(PingPong(), 0.5f, 1, 1, 0, 0), Scored(Identity(), 0.4f, 1, 0, 0.5f, 1) });

            Assert.Equal(new[] { 2 }, diagnosis.Unsolved);
            Assert.Equal(2, diagnosis.Frontier);
            Assert.StartsWith("No network yet does gap 3 (5)", diagnosis.Describe(new SequenceTask("fib", new[] { 1, 2, 5, 8 })));
        }
    }
}
