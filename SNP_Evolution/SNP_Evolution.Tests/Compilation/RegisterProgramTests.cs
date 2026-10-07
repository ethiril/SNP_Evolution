using SnpEvolution.Compilation;

namespace SnpEvolution.Tests.Compilation
{
    public class RegisterProgramTests
    {
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
            RegisterProgram evens = RegisterProgram.Parse("ADD r0 -> 1\nADD r0 -> 0 | 2\nHALT");

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
            RegisterProgram evens = RegisterProgram.Parse("ADD r0 -> 1\nADD r0 -> 0 | 2\nHALT");

            (IReadOnlyList<int> outputs, bool complete, _) = evens.Generate(outputLimit: 7);

            Assert.Equal(new[] { 2, 4, 6 }, outputs);
            Assert.True(complete);
        }
    }
}
