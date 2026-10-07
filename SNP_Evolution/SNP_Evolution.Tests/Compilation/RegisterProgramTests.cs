using SnpEvolution.Compilation;
using SnpEvolution.Search;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Tests.Fixtures;

namespace SnpEvolution.Tests.Compilation
{
    public class RegisterProgramTests
    {
        private const string EvensProgram = "ADD r0 -> 1\nADD r0 -> 0 | 2\nHALT";

        [Fact]
        public void ProgramsReadAndWriteTheSameWay()
        {
            RegisterProgram program = RegisterProgram.Parse("0: ADD r1 -> 1 | 2\n1: SUB r1 -> 2 else 3\n2: ADD r0 -> 3\n3: HALT\n");

            Assert.Equal(program.ToString(), RegisterProgram.Parse(program.ToString()).ToString());
            Assert.Equal(2, program.RegisterCount);
        }

        [Fact]
        public void ProgramsGenerateEveryNumberAlongEveryChoice()
        {
            RegisterProgram evens = RegisterProgram.Parse(EvensProgram);

            (IReadOnlyList<int> outputs, bool complete, _) = evens.Generate(maxSteps: 20);

            Assert.Equal(new[] { 2, 4, 6, 8, 10, 12 }, outputs.Take(6));
            Assert.False(complete);
        }

        [Fact]
        public void TheOutputRegisterCannotBeSubtractedFrom()
        {
            Assert.NotNull(RegisterProgram.Parse("SUB r0 -> 1 else 1\nHALT").Problem());
        }

        // Only the first numbers are generated before the limit on register 0 cuts the computations off, and that
        // still counts as complete, since nothing at or below the limit is lost.
        [Fact]
        public void TheOutputLimitLosesNothingBelowIt()
        {
            RegisterProgram evens = RegisterProgram.Parse(EvensProgram);

            (IReadOnlyList<int> outputs, bool complete, _) = evens.Generate(outputLimit: 7);

            Assert.Equal(new[] { 2, 4, 6 }, outputs);
            Assert.True(complete);
        }
    
        public static TheoryData<string> FunctionContracts => new TheoryData<string>(FunctionPrograms.Texts.Keys);

        [Theory]
        [MemberData(nameof(FunctionContracts))]
        public void AHandWrittenFunctionProgramPassesItsContractsCases(string name)
        {
            Contract contract = FirstParts.Named(name);
            FunctionProgram program = FunctionPrograms.For(name);

            Assert.Null(program.Problem());
            Assert.All(contract.Cases, @case =>
            {
                FunctionRun run = program.Run(@case.Inputs, FunctionScoring.StepsFor(@case));
                Assert.True(run.Complete);
                FunctionOutcome outcome = Assert.Single(run.Outcomes);
                Assert.Equal(@case.Done, outcome.Done);
                Assert.Equal(@case.Outputs.OrderBy(pair => pair.Key), outcome.Outputs.OrderBy(pair => pair.Key));
                Assert.True(outcome.Clean);
            });
        }

        [Fact]
        public void AFunctionProgramNamesItsDonePortsAndReadsBackTheSame()
        {
            FunctionProgram program = FunctionPrograms.For("zero test");

            Assert.Equal(new[] { 0, 1 }, program.Program.Instructions.Where(instruction => instruction.Operation == Operation.Halt).Select(instruction => instruction.Register).Order());
            Assert.Contains("3: HALT zero", program.ToString());
            Assert.Equal(program.Program.ToString(), RegisterProgram.Parse(program.Program.ToString()).ToString());
        }

        [Fact]
        public void AnInputLeftInItsRegisterIsNotClean()
        {
            FunctionProgram copy = FunctionProgram.Parse("ADD r1 -> 1\nHALT", new[] { "n" }, new[] { "out" }, new[] { "done" });

            FunctionOutcome outcome = Assert.Single(copy.Run(new Dictionary<string, int> { ["n"] = 2 }, 50).Outcomes);

            Assert.False(outcome.Clean);
            Assert.Equal(1, outcome.Outputs["out"]);
        }

        [Fact]
        public void AProgramThatLoopsForeverDoesNotComplete()
        {
            FunctionProgram loop = FunctionProgram.Parse("SUB r1 -> 0 else 0\nHALT", new[] { "n" }, new[] { "out" }, new[] { "done" });

            FunctionRun run = loop.Run(new Dictionary<string, int> { ["n"] = 1 }, 50);

            Assert.False(run.Complete);
            Assert.Empty(run.Outcomes);
        }

        [Fact]
        public void AChoiceIsFollowedBothWays()
        {
            FunctionProgram either = FunctionProgram.Parse("ADD r0 -> 1 | 2\nADD r0 -> 2\nHALT", Array.Empty<string>(), new[] { "out" }, new[] { "done" });

            Assert.Equal(new[] { 1, 2 }, either.Run(new Dictionary<string, int>(), 50).Outcomes.Select(outcome => outcome.Outputs["out"]).Order());
        }
    }
}
