using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Search.Operators;
using static SnpEvolution.Tests.Fixtures.ModuleFixtures;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Search.Operators
{
    public class LexicaseSelectionTests
    {
        [Fact]
        public void LexicasePicksSpecialistsButNeverANetworkThatIsBestAtNothing()
        {
            var random = new Random(1);
            List<Individual> ranked = Ranking.Rank(new[]
            {
                Scored(PingPong(), 0.6f, 0.6f, 0.6f),
                Scored(Identity(), 0.5f, 1f, 0f),
                Scored(NeverOutputs(), 0.5f, 0f, 1f),
                Scored(Chain(), 0.4f, 0.5f, 0.5f),
            });
            Func<Individual> pick = new LexicaseSelection().Prepare(ranked, random);

            List<Individual> picks = Enumerable.Range(0, 200).Select(_ => pick()).ToList();

            Assert.Contains(picks, individual => individual.Fitness == 0.5f && individual.Checks[1] == 1f);
            Assert.Contains(picks, individual => individual.Fitness == 0.5f && individual.Checks[0] == 1f);
            Assert.DoesNotContain(picks, individual => individual.Fitness == 0.4f);
        }
    }
}
