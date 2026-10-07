using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Verification;

namespace SnpEvolution.Tests.Specs.Verification
{
    public class RobustnessTests
    {
        [Theory]
        [MemberData(nameof(PartFixtures.HandBuilt), MemberType = typeof(PartFixtures))]
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
        public void CellsAreRobustnessInTenthsByNeurons()
        {
            Part part = ReferenceParts.Delay(2);

            Assert.Equal((10, part.Network.Neurons.Count), Robustness.Cells(part.Task(), jitter: 1, budget: new EvaluationBudget())(part.Network));
            Assert.Equal(9, Robustness.Tenths(0.95f));
            Assert.Equal(10, Robustness.Tenths(1f));
        }
    }
}
