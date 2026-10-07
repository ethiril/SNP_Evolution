using SnpEvolution.Application;
using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Search;
using SnpEvolution.Evolution.Verification;
using SnpEvolution.Simulation;

namespace SnpEvolution.Tests.Evolution
{
    public class RobustnessTests
    {
        public static IEnumerable<object[]> HandBuilt => Enumerable.Range(0, HandBuiltParts.All().Count).Select(index => new object[] { index });

        [Theory]
        [MemberData(nameof(HandBuilt))]
        public void EveryHandBuiltPartIsRobustWithoutJitter(int index)
        {
            Assert.Equal(1f, Robustness.Of(HandBuiltParts.All()[index], jitter: 0, budget: new EvaluationBudget(), runs: 10));
        }

        // A delay part is all timing, but its done still fires once and it still empties, so jitter only moves done.
        [Fact]
        public void ADelayKeepsItsContractLatencyAside()
        {
            Assert.Equal(1f, Robustness.Of(ReferenceParts.Delay(3), jitter: 2, budget: new EvaluationBudget(), runs: 20));
        }

        // The store must tell a draining count from a loaded one by spikes arriving together, which late spikes break.
        [Fact]
        public void APartThatNeedsSpikesToArriveTogetherBreaksUnderJitter()
        {
            Assert.InRange(Robustness.Of(PartFixtures.Register(), jitter: 1, budget: new EvaluationBudget(), runs: 50), 0f, 0.9f);
        }

        [Fact]
        public void TheSameSeedGivesTheSameScore()
        {
            var budget = new EvaluationBudget();

            Assert.Equal(Robustness.Of(ReferenceParts.Add(), jitter: 1, budget, runs: 20), Robustness.Of(ReferenceParts.Add(), jitter: 1, budget, runs: 20));
            Assert.Equal(2, budget[EvaluationKind.JitterRun]);
            Assert.Equal(0, budget.Networks);
        }

        [Fact]
        [Slow]
        public void ARobustShrinkKeepsAVerifiedPartAtLeastAsRobustAsTheOneFound()
        {
            var settings = new PartSearchSettings(5_000, 1_000, 30, Catalog.StructuralDefault, () => new ExhaustiveCpuEngine(), RobustJitter: 1);

            PartOutcome outcome = PartSearch.Evolve(PartFixtures.DelayContract(2), 1, settings, new EvaluationBudget(), _ => { });

            Assert.True(outcome.Solved);
            Assert.IsType<Verdict.Passed>(outcome.Measurement!.Verdict);
            Assert.Equal(10, Robustness.Tenths(Robustness.Of(outcome.Part!, jitter: 1, budget: new EvaluationBudget(), runs: Robustness.CellRuns)));
        }

        [Fact]
        public void CellsAreRobustnessInTenthsByNeurons()
        {
            Part part = ReferenceParts.Delay(2);

            Assert.Equal((10, part.Network.Neurons.Count), Robustness.Cells(part.Task(), jitter: 1, budget: new EvaluationBudget())(part.Network));
            Assert.Equal(9, Robustness.Tenths(0.95f));
            Assert.Equal(10, Robustness.Tenths(1f));
        }
    }
}
