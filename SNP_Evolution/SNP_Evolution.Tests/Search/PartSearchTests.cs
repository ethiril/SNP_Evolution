using SnpEvolution.Model;
using SnpEvolution.Search;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Verification;

namespace SnpEvolution.Tests.Search
{
    public class PartSearchTests
    {
        [Fact]
        public void HardwareProfileIsOffUnlessAskedFor()
        {
            Assert.False(new PartSearchSettings(1, 1, 1, SearchCatalog.Evolution[0], () => null!).HardwareProfile);
        }

        [Fact]
        public void PartSearchUnderTheProfileFindsAndShrinksAProfilePart()
        {
            var settings = new PartSearchSettings(5_000, 1_000, 30, SearchCatalog.StructuralDefault, () => new ExhaustiveCpuEngine(), HardwareProfile: true);

            PartOutcome outcome = PartSearch.Evolve(PartFixtures.DelayContract(2), 1, settings, new EvaluationBudget(), _ => { });

            Assert.True(outcome.Solved);
            Assert.Empty(HardwareProfile.Problems(outcome.Part!.Network));
        }

        [Fact]
        [Slow]
        public void ARobustShrinkKeepsAVerifiedPartAtLeastAsRobustAsTheOneFound()
        {
            var settings = new PartSearchSettings(20_000, 1_000, 30, SearchCatalog.StructuralDefault, () => new ExhaustiveCpuEngine(), RobustJitter: 1);
            static int Tenths(PartOutcome outcome) => Robustness.Tenths(Robustness.Of(outcome.Part!, jitter: 1, budget: new EvaluationBudget(), runs: Robustness.CellRuns));

            // Without a shrink budget the part kept is the one the search found, as the shrink comes after the search.
            PartOutcome found = PartSearch.Evolve(PartFixtures.DelayContract(2), 1, settings with { ShrinkBudget = 0 }, new EvaluationBudget(), _ => { });
            PartOutcome outcome = PartSearch.Evolve(PartFixtures.DelayContract(2), 1, settings, new EvaluationBudget(), _ => { });

            Assert.True(found.Solved);
            Assert.True(outcome.Solved);
            Assert.IsType<Verdict.Passed>(outcome.Measurement!.Verdict);
            Assert.True(Tenths(outcome) >= Tenths(found), $"kept {Tenths(outcome)}/10, found {Tenths(found)}/10");
        }
    }
}
