using System.Diagnostics;
using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Evolution.Verification;
using SnpEvolution.Networks;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Evolution
{
    public class BoundedCheckTests
    {
        private static readonly ProofLimits AMinute = new ProofLimits(TimeSpan.FromMinutes(1));

        // The hand-built register, except that a store holding 41 spikes (n = 20 and start) sends two spikes instead of one.
        internal static Part RegisterFailingAtTwenty()
        {
            Part register = HandBuiltParts.All().Single(part => part.Contract.Name == "register");
            const int Store = 4;
            List<Neuron> neurons = register.Network.Neurons.ToList();
            neurons[Store] = new Neuron(
                new[] { Rule.Standard("a(aa){1,19}|a(aa){21,}", 2), Rule.Standard("a{41}", 2, 2), Rule.Standard("a", 1, 2) }, 0, neurons[Store].Connections, false);
            return register with { Network = new Network(neurons) };
        }

        [Fact]
        public void EveryKnownContractFindsTheSpecificationItIsBuiltFrom()
        {
            Assert.All(ArithmeticParts.KnownEntries, entry => Assert.Same(entry.Specification, Specification.For(entry.Contract)));
        }


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
            Part broken = RegisterFailingAtTwenty();
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

            Assert.IsType<Verdict.Failed>(BoundedCheck.Admit(RegisterFailingAtTwenty(), new EvaluationBudget(), log.Add).Verdict);
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

        [Fact]
        public void ALibraryFileKeepsItsProvenBound()
        {
            Part delay = ReferenceParts.Delay(2);
            LibraryPart part = Verifier.Measure(delay, new EvaluationBudget()).ToLibraryPart(delay, new PartOrigin(0, "by hand", 0)) with { Proven = new ProvenBound(0, true, new StopReason(Stop.EveryInputChecked)) };

            LibraryPart read = PartLibraryFiles.Read(PartLibraryFiles.ToJson(part), "delay-2.json");

            Assert.Equal(part.Proven, read.Proven);
            Assert.Contains("\"Stopped\": \"every input checked\"", PartLibraryFiles.ToJson(part));
        }

        // Part files keep the reason as text, so the files written before it was typed read back as the same reason.
        [Theory]
        [InlineData("every input checked")]
        [InlineData("bound 6 reached")]
        [InlineData("time limit of 10 s")]
        [InlineData("n=3 has too many computations to follow exactly")]
        [InlineData("counterexample at a=14,b=0,n=0")]
        [InlineData("the contract has no specification")]
        public void AStopReasonReadsBackFromItsText(string text)
        {
            StopReason reason = StopReason.Parse(text);

            Assert.NotEqual(Stop.Other, reason.Stop);
            Assert.Equal(text, reason.ToString());
        }

        // CEGIS adds a counterexample's case to the cases a search scores on.
        [Fact]
        [Slow]
        public void ACounterexampleIsACaseAContractCanTake()
        {
            Part broken = RegisterFailingAtTwenty();
            Counterexample counterexample = Assert.IsType<Verdict.Failed>(BoundedCheck.Prove(broken, AMinute, new EvaluationBudget()).Verdict).Counterexample;

            Contract withIt = broken.Contract with { Cases = broken.Contract.Cases.Append(counterexample.Case).ToList(), MaxLatency = counterexample.Contract.MaxLatency };

            Assert.IsType<Verdict.Failed>(new Verifier(new ContractTask(withIt, broken.Binding), new EvaluationBudget()).Check(broken.Network));
        }

        [Fact]
        [Slow]
        public void ALibraryFileWithACounterexampleIsRefused()
        {
            Part broken = RegisterFailingAtTwenty();
            LibraryPart part = Verifier.Measure(broken, new EvaluationBudget()).ToLibraryPart(broken, new PartOrigin(0, "by hand", 0)) with { Proven = BoundedCheck.Prove(broken, AMinute, new EvaluationBudget()).Proven };

            var refusal = Assert.Throws<InvalidDataException>(() => PartLibraryFiles.Read(PartLibraryFiles.ToJson(part), "register.json"));

            Assert.Contains("n=20", refusal.Message);
        }
    }
}
