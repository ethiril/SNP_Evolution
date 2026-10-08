using SnpEvolution.Compilation;
using SnpEvolution.Model;
using SnpEvolution.Search;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;
using SnpEvolution.Specs.Verification;
using SnpEvolution.Storage;
using SnpEvolution.Tests.Fixtures;

namespace SnpEvolution.Tests.Search
{
    public class CompiledPartSearchTests
    {
        private static readonly PartSearchSettings Small = new PartSearchSettings(500, 500, 20, SearchCatalog.StructuralDefault, () => new ExhaustiveCpuEngine());

        public static TheoryData<string> FunctionContracts => new TheoryData<string>(FunctionPrograms.Texts.Keys);

        // Each HALT becomes one loop per output, in order, ending on a HALT for the same done port.
        [Fact]
        public void EachHaltDrainsTheOutputsInTurnBeforeItsDone()
        {
            (RegisterProgram drained, IReadOnlyDictionary<int, int> drains) = RegisterMachineCompiler.WithDrains(FunctionPrograms.For("fan-out"));

            Assert.Equal("0: SUB r0 -> 1 else 3\n1: ADD r1 -> 2\n2: ADD r2 -> 0\n3: SUB r1 -> 3 else 4\n4: SUB r2 -> 4 else 5\n5: HALT\n", drained.ToString().ReplaceLineEndings("\n"));
            Assert.Equal(new Dictionary<int, int> { [3] = 0, [4] = 1 }, drains);
        }

        // The interpreter's latency is the one the compiled network has on every case.
        [Theory]
        [MemberData(nameof(FunctionContracts))]
        public void TheInterpreterGivesTheCompiledPartsLatency(string name)
        {
            Contract contract = FirstParts.Named(name);
            FunctionProgram program = FunctionPrograms.For(name);
            var task = new ContractTask(contract);
            IReadOnlyList<TrialResult> results = new Verifier(task, new EvaluationBudget()).Run(RegisterMachineCompiler.Compile(program));

            Assert.All(Enumerable.Range(0, contract.Cases.Count), index =>
                Assert.Equal(task.Latency(results[index].PortRuns[0], index), program.Run(contract.Cases[index].Inputs, FunctionScoring.StepsFor(contract.Cases[index])).Outcomes.Single().Latency));
        }

        // The program search then has to pass the input the bounded check failed on, with the latency allowed there.
        [Fact]
        [Slow]
        public void ACounterexampleBecomesACaseTheProgramSearchMustPass()
        {
            Part broken = PartFixtures.RegisterFailingAtTwenty();
            Counterexample counterexample = Assert.IsType<Verdict.Failed>(BoundedCheck.Prove(broken, new ProofLimits(TimeSpan.FromMinutes(1)), new EvaluationBudget()).Verdict).Counterexample;

            ContractTask widened = CompiledPartSearch.WithCase(broken.Task(), counterexample);

            Assert.Equal(broken.Contract.Cases.Count + 1, widened.Contract.Cases.Count);
            Assert.Equal(20, widened.Contract.Cases[^1].Inputs["n"]);
            Assert.Equal(Math.Max(broken.Contract.MaxLatency, counterexample.Contract.MaxLatency), widened.Contract.MaxLatency);
            Assert.Equal(widened.Contract.Cases.Count * new FunctionScoring(widened.Contract, 2_000, new EvaluationBudget()).ChecksPerCase,
                new FunctionScoring(widened.Contract, 2_000, new EvaluationBudget()).Score(FunctionPrograms.For("register").Program).Checks.Count);
        }

        [Fact]
        public void OnlyContractsWithCountPortsAreCompiled()
        {
            Assert.True(CompiledPartSearch.Applies(FirstParts.Named("zero test")));
            Assert.False(CompiledPartSearch.Applies(FirstParts.Named("delay 2")));
            Assert.False(CompiledPartSearch.Applies(FirstParts.Named("join")));
        }

        [Theory]
        [Slow]
        [MemberData(nameof(FunctionContracts))]
        public void AHandWrittenProgramCompilesToAPartProvenPastItsCases(string name)
        {
            Contract contract = FirstParts.Named(name);
            var part = new Part(contract, RegisterMachineCompiler.Compile(FunctionPrograms.For(name)), PortLayout.AfterInputs(contract));

            BoundedResult admission = BoundedCheck.Admit(part, new EvaluationBudget(), _ => { });

            Assert.IsNotType<Verdict.Failed>(admission.Verdict);
            Assert.True(admission.Proven.UpTo >= contract.Cases.SelectMany(@case => @case.Inputs.Values).Max(), admission.Proven.ToString());
        }

        [Fact]
        public void TheShortestProgramLeavesOutInstructionsNoCaseReaches()
        {
            Contract register = FirstParts.Named("register");
            RegisterProgram padded = RegisterProgram.Parse("SUB r0 -> 1 else 3\nADD r1 -> 0\nADD r2 -> 2\nHALT\nSUB r2 -> 4 else 0");

            RegisterProgram shortest = CompiledPartSearch.Shortest(padded, new FunctionScoring(register, 2_000, new EvaluationBudget()), new ContractTask(register));

            Assert.Equal(3, shortest.Instructions.Count);
        }

        [Fact]
        public void TheCompileRouteFindsVerifiesAndShrinksARegister()
        {
            var budget = new EvaluationBudget();

            PartOutcome outcome = CompiledPartSearch.Compile(FirstParts.Named("register"), 1, Small, 200, budget, _ => { });

            Assert.True(outcome.Solved);
            Assert.NotNull(outcome.Compiled);
            Assert.True(HardwareCost.Of(outcome.Part!.Network).CompareTo(outcome.Compiled!.Compiled) <= 0);
            Assert.True(budget[EvaluationKind.InterpreterRun] > 0);
            Assert.IsType<Verdict.Passed>(Verifier.Measure(outcome.Part, new EvaluationBudget()).Verdict);
        }

        [Fact]
        public void APartFileFromTheCompilerLoadsVerifiesAndNamesItsProgram()
        {
            Contract contract = FirstParts.Named("increment");
            FunctionProgram program = FunctionPrograms.For("increment");
            var part = new Part(contract, RegisterMachineCompiler.Compile(program), PortLayout.AfterInputs(contract));
            LibraryPart saved = Verifier.Measure(part, new EvaluationBudget()).ToLibraryPart(part, new PartOrigin(1, "evolve-parts --route compile", 10, program.ToString(), HardwareCost.Of(part.Network)));

            LibraryPart read = PartLibraryFiles.Read(PartLibraryFiles.ToJson(saved), "increment.json");

            Assert.Equal(program.ToString(), read.Origin.Program);
            Assert.Equal(HardwareCost.Of(part.Network), read.Origin.Compiled);
        }

        [Fact]
        public void AnEvolvedPartFileSaysNothingOfAProgram()
        {
            string json = PartLibraryFiles.ToJson(Verifier.Measure(ReferenceParts.Delay(2), new EvaluationBudget()).ToLibraryPart(ReferenceParts.Delay(2), new PartOrigin(1, "evolve-parts", 10)));

            Assert.DoesNotContain("Program", json);
            Assert.DoesNotContain("Compiled", json);
        }
    }
}
