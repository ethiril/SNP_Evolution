using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Verification;

namespace SnpEvolution.Tests.Specs.Accounting
{
    public class EvaluationBudgetTests
    {
        [Fact]
        public void APhaseChargesItsParentAndStopsAtItsOwnLimit()
        {
            var run = new EvaluationBudget(limit: 100);
            EvaluationBudget phase = run.Phase(10, EvaluationSource.Proposals);

            phase.Charge(EvaluationKind.Network, 10, EvaluationSource.Main);
            phase.Charge(EvaluationKind.ExhaustiveCheck, 3);

            Assert.True(phase.IsSpent);
            Assert.False(run.IsSpent);
            Assert.Equal(10, run[EvaluationSource.Proposals]);
            Assert.Equal(0, run[EvaluationSource.Main]);
            Assert.Equal(3, run[EvaluationKind.ExhaustiveCheck]);
        }

        [Fact]
        public void OnlyNetworkEvaluationsCountTowardsTheLimit()
        {
            var budget = new EvaluationBudget(limit: 5);

            budget.Charge(EvaluationKind.ExhaustiveCheck, 50);
            budget.Charge(EvaluationKind.ProofStep, 50);
            budget.Charge(EvaluationKind.InterpreterRun, 50);
            budget.Charge(EvaluationKind.JitterRun, 50);
            Assert.False(budget.IsSpent);

            budget.Charge(EvaluationKind.Network, 5);
            Assert.True(budget.IsSpent);
        }

        [Fact]
        public void WithoutALimitNothingIsSpent()
        {
            var budget = new EvaluationBudget();
            budget.Charge(EvaluationKind.Network, long.MaxValue / 2);

            Assert.False(budget.IsSpent);
            Assert.True(new EvaluationBudget().Phase(0).IsSpent);
        }

        [Fact]
        public void TheReportNamesOtherKindsOnlyWhenSomeWereSpent()
        {
            var budget = new EvaluationBudget();
            budget.Charge(EvaluationKind.Network, 2_500);
            Assert.Equal("Evaluations: 2,500 main run, 0 side runs, 0 incubation, 0 verification, 0 proposed parts; 2,500 in all.", budget.Report().Describe());

            budget.Charge(EvaluationKind.ExhaustiveCheck, 7);
            Assert.EndsWith(" Besides them, 7 exhaustive checks.", budget.Report().Describe());
        }

        [Fact]
        public void APhaseCountsItsRepeatsInItsParentAndTheReportNamesThem()
        {
            var run = new EvaluationBudget();
            run.Phase().CountRepeats(1_200, 300);

            Assert.Equal(1_200, run.Report().Repeats);
            Assert.Contains(" 1,200 repeated a network already scored on the same task, 300 of them exactly.", run.Report().Describe());
        }

        [Fact]
        public void AVerifierChargesOneExhaustiveCheckPerNetworkItRuns()
        {
            var budget = new EvaluationBudget();

            Verifier.Measure(ReferenceParts.Delay(2), budget);

            Assert.Equal(1, budget[EvaluationKind.ExhaustiveCheck]);
            Assert.Equal(0, budget.Networks);
        }
    }
}
