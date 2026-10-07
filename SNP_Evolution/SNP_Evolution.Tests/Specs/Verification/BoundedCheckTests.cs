using System.Diagnostics;
using SnpEvolution.Model;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;
using SnpEvolution.Specs.Verification;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Specs.Verification
{
    public class BoundedCheckTests
    {
        private static readonly ProofLimits AMinute = new ProofLimits(TimeSpan.FromMinutes(1));


        [Fact]
        [Slow]
        public void AContractThatOnlySharesAKnownNameIsNotProven()
        {
            Part register = ReferenceParts.Register();
            Contract doubled = register.Contract with { Cases = register.Contract.Cases.Select(@case => @case with { Outputs = new Dictionary<string, int> { ["out"] = 2 * @case.Outputs["out"] } }).ToList() };

            BoundedResult result = BoundedCheck.Prove(register with { Contract = doubled }, AMinute, new EvaluationBudget());

            Assert.Equal(-1, result.Proven.UpTo);
            Assert.Equal(Stop.NoSpecification, Assert.IsType<Verdict.Unknown>(result.Verdict).Reason.Stop);
        }

        [Fact]
        [Slow]
        public void ProvesTheRegisterReferencePartToAtLeast32WithinAMinute()
        {
            var clock = Stopwatch.StartNew();

            BoundedResult result = BoundedCheck.Prove(ReferenceParts.Register(), AMinute with { MaxBound = 32 }, new EvaluationBudget());

            Assert.IsNotType<Verdict.Failed>(result.Verdict);
            Assert.Equal(32, result.Proven.UpTo);
            Assert.True(clock.Elapsed < TimeSpan.FromMinutes(1), $"took {clock.Elapsed}");
        }

        [Fact]
        [Slow]
        public void CatchesAPartThatFailsOnlyAtTwentyWithItsTrace()
        {
            Part broken = PartFixtures.RegisterFailingAtTwenty();
            Assert.IsType<Verdict.Passed>(Verifier.Measure(broken, new EvaluationBudget()).Verdict);

            var budget = new EvaluationBudget();
            BoundedResult result = BoundedCheck.Prove(broken, AMinute, budget);

            Assert.Equal(19, result.Proven.UpTo);
            Assert.InRange(budget[EvaluationKind.ProofStep], 1, 21);
            Assert.Equal(budget[EvaluationKind.ProofStep], budget[EvaluationKind.ExhaustiveCheck]);
            Assert.Equal("n=20", result.Proven.FailsAt);
            Counterexample counterexample = Assert.IsType<Verdict.Failed>(result.Verdict).Counterexample;
            Assert.Equal("n=20", counterexample.Inputs);
            Assert.Equal(20, counterexample.Case.Outputs["out"]);
            Assert.NotEqual("done,out=20", counterexample.Read);
            string[] text = CounterexampleText.Of(broken, counterexample).Split('\n');
            Assert.StartsWith("Counterexample at n=20:", text[0]);
            Assert.Contains("Expected done,out=20", text[0]);
            Assert.StartsWith("Spikes held after each step", text[1]);
            Assert.Contains("start", text[2]);
        }

        [Fact]
        [Slow]
        public void ANondeterministicCounterexampleShowsTheFailingComputation()
        {
            Part delay = ReferenceParts.Delay(2);
            Neuron start = delay.Network.Neurons[0];
            var sometimesTwice = delay with { Network = new Network(new[] { start.WithRules(new[] { Rule.Standard("a", 1, 1), Rule.Standard("a", 1, 2) }), delay.Network.Neurons[1] }) };

            Counterexample counterexample = Assert.IsType<Verdict.Failed>(BoundedCheck.Prove(sometimesTwice, AMinute, new EvaluationBudget()).Verdict).Counterexample;

            Assert.Equal(ContractRule.DoneOnce, counterexample.Rule);
            Assert.Equal(2, counterexample.Run.Firings.Last().Count);
            Assert.StartsWith("Port firings of the failing computation", CounterexampleText.Of(sometimesTwice, counterexample).Split('\n')[1]);
        }

        [Fact]
        public void APartIsNotAdmittedWhenItFailsWithinTwiceItsLargestCase()
        {
            var log = new List<string>();

            Assert.IsType<Verdict.Failed>(BoundedCheck.Admit(PartFixtures.RegisterFailingAtTwenty(), new EvaluationBudget(), log.Add).Verdict);
            Assert.Contains(log, line => line.Contains("Not admitted") && line.Contains("n=20"));
            Assert.Equal(24, BoundedCheck.Admission(FirstParts.Named("register")).MaxBound);
        }

        [Fact]
        [Slow]
        public void APartWithNoDataInPortsIsProvenForEveryInput()
        {
            BoundedResult result = BoundedCheck.Prove(ReferenceParts.Delay(2), AMinute, new EvaluationBudget());

            Assert.True(result.Proven.AllInputs);
            Assert.Equal(0, result.Proven.UpTo);
            Assert.IsType<Verdict.Passed>(result.Verdict);
        }

        [Fact]
        [Slow]
        public void ACaseTooWideToFollowExactlyIsNotProven()
        {
            BoundedResult result = BoundedCheck.Prove(ReferenceParts.Register(), AMinute with { MaxConfigurations = 0 }, new EvaluationBudget());

            Assert.Equal(-1, result.Proven.UpTo);
            Assert.Equal(Stop.TooWide, Assert.IsType<Verdict.Unknown>(result.Verdict).Reason.Stop);
            Assert.Contains("too many computations", result.Proven.Stopped.ToString());
        }

        [Fact]
        [Slow]
        public void EveryHandBuiltPartIsProvenToItsAdmissionBound()
        {
            Assert.All(HandBuiltParts.All(), part =>
            {
                BoundedResult result = BoundedCheck.Prove(part, BoundedCheck.Admission(part.Contract) with { Time = TimeSpan.FromMinutes(1) }, new EvaluationBudget());

                Assert.True(result.Verdict is not Verdict.Failed, $"{part.Contract.Name}: {result.Verdict}");
                Assert.Equal(BoundedCheck.Admission(part.Contract).MaxBound, result.Proven.UpTo);
            });
        }

        [Fact]
        public void InputsAtABoundHaveThatLargestValueAndIntervalsStartAtOne()
        {
            Assert.Equal(16 - 9, BoundedInputs.WithLargest(FirstParts.Named("add"), 3).Count());
            Assert.Empty(BoundedInputs.WithLargest(FirstParts.Named("interval to count"), 0));
            Assert.Equal(new[] { 1 }, BoundedInputs.WithLargest(FirstParts.Named("interval to count"), 1).Select(inputs => inputs["n"]));
        }

        // CEGIS adds a counterexample's case to the cases a search scores on.
        [Fact]
        [Slow]
        public void ACounterexampleIsACaseAContractCanTake()
        {
            Part broken = PartFixtures.RegisterFailingAtTwenty();
            Counterexample counterexample = Assert.IsType<Verdict.Failed>(BoundedCheck.Prove(broken, AMinute, new EvaluationBudget()).Verdict).Counterexample;

            Contract withIt = broken.Contract with { Cases = broken.Contract.Cases.Append(counterexample.Case).ToList(), MaxLatency = counterexample.Contract.MaxLatency };

            Assert.IsType<Verdict.Failed>(new Verifier(new ContractTask(withIt, broken.Binding), new EvaluationBudget()).Check(broken.Network));
        }

        [Fact]
        [Slow]
        public void ALibraryFileWithACounterexampleIsRefused()
        {
            Part broken = PartFixtures.RegisterFailingAtTwenty();
            LibraryPart part = PartFixtures.Measured(broken, new PartOrigin(0, "by hand", 0)) with { Proven = BoundedCheck.Prove(broken, AMinute, new EvaluationBudget()).Proven };

            var refusal = Assert.Throws<InvalidDataException>(() => PartLibraryFiles.Read(PartLibraryFiles.ToJson(part), "register.json"));

            Assert.Contains("n=20", refusal.Message);
        }
    }
}
