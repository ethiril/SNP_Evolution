using SnpEvolution.Cli;
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
            Assert.Equal(1f, Robustness.Of(HandBuiltParts.All()[index], jitter: 0, runs: 10));
        }

        // A delay part is all timing, but its done still fires once and it still empties, so jitter only moves done.
        [Fact]
        public void ADelayKeepsItsContractLatencyAside()
        {
            Assert.Equal(1f, Robustness.Of(ReferenceParts.Delay(3), jitter: 2, runs: 20));
        }

        // The store must tell a draining count from a loaded one by spikes arriving together, which late spikes break.
        [Fact]
        public void APartThatNeedsSpikesToArriveTogetherBreaksUnderJitter()
        {
            Assert.InRange(Robustness.Of(PartFixtures.Register(), jitter: 1, runs: 50), 0f, 0.9f);
        }

        [Fact]
        public void TheSameSeedGivesTheSameScore()
        {
            Assert.Equal(Robustness.Of(ReferenceParts.Add(), jitter: 1, runs: 20), Robustness.Of(ReferenceParts.Add(), jitter: 1, runs: 20));
        }

        [Fact]
        [Slow]
        public void ARobustShrinkKeepsAVerifiedPartAtLeastAsRobustAsTheOneFound()
        {
            var settings = new PartSearchSettings(5_000, 1_000, 30, Catalog.ChoiceFor(Catalog.StructuralDefault), () => new ExhaustiveCpuEngine(), RobustJitter: 1);

            PartOutcome outcome = PartEvolution.Evolve(PartFixtures.DelayContract(2), 1, settings, _ => { });

            Assert.True(outcome.Solved);
            Assert.IsType<Verdict.Passed>(outcome.Measurement!.Verdict);
            Assert.Equal(10, Robustness.Tenths(Robustness.Of(outcome.Part!, jitter: 1, runs: Robustness.CellRuns)));
        }

        [Fact]
        public void CellsAreRobustnessInTenthsByNeurons()
        {
            Part part = ReferenceParts.Delay(2);

            Assert.Equal((10, part.Network.Neurons.Count), Robustness.Cells(part.Task(), jitter: 1)(part.Network));
            Assert.Equal(9, Robustness.Tenths(0.95f));
            Assert.Equal(10, Robustness.Tenths(1f));
        }
    }
}
