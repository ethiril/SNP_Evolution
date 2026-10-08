using SnpEvolution.Compilation;
using SnpEvolution.Model;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Tasks;
using SnpEvolution.Tests.Fixtures;
using static SnpEvolution.Tests.Fixtures.Runs;

namespace SnpEvolution.Tests.Compilation
{
    public class RegisterMachineCompilerTests
    {
        // Outputs a, then (a, b) becomes (b, a + b): every Fibonacci number, with r4 only there to make the choice.
        private const string FibonacciProgram = @"
            0: ADD r1 -> 1
            1: ADD r2 -> 2
            2: ADD r4 -> 3 | 5
            3: SUB r1 -> 4 else 12
            4: ADD r0 -> 3
            5: SUB r1 -> 6 else 7
            6: ADD r3 -> 5
            7: SUB r2 -> 8 else 10
            8: ADD r1 -> 9
            9: ADD r3 -> 7
            10: SUB r3 -> 11 else 2
            11: ADD r2 -> 10
            12: HALT";

        [Theory]
        [InlineData("ADD r0 -> 1\nADD r0 -> 2\nADD r0 -> 3\nHALT", new[] { 3 })]
        [InlineData("ADD r0 -> 1\nADD r0 -> 2 | 3\nADD r0 -> 3\nHALT", new[] { 2, 3 })]
        // Counts 2 or 3 into r1, then moves it into r0 one at a time.
        [InlineData("ADD r1 -> 1\nADD r1 -> 2 | 3\nADD r1 -> 3\nSUB r1 -> 4 else 5\nADD r0 -> 3\nHALT", new[] { 2, 3 })]
        // Two registers subtracted from by different instructions, with a choice inside the loop.
        [InlineData("ADD r1 -> 1\nADD r2 -> 2 | 3\nADD r2 -> 3\nSUB r2 -> 4 else 5\nADD r0 -> 3\nSUB r1 -> 6 else 7\nADD r0 -> 5\nHALT", new[] { 2, 3 })]
        public void CompiledProgramsGenerateWhatTheProgramDoes(string text, int[] expected)
        {
            RegisterProgram program = RegisterProgram.Parse(text);
            Assert.Equal(expected, program.Generate().Outputs);

            Assert.Equal(expected, Generated(RegisterMachineCompiler.Compile(program)));
        }

        [Fact]
        public void ACompiledFibonacciProgramGeneratesTheSameNumbersAsTheProgram()
        {
            RegisterProgram program = RegisterProgram.Parse(FibonacciProgram);
            (IReadOnlyList<int> outputs, _, int steps) = program.Generate(outputLimit: 21, valueLimit: 100);

            // A generous bound on the network steps the compiled program needs to output 21 and count down its halt.
            int networkSteps = 4 * (steps + 2) + 31;
            IReadOnlyList<int> generated = Generated(RegisterMachineCompiler.Compile(program), networkSteps);

            Assert.Equal(new[] { 1, 2, 3, 5, 8, 13, 21 }, outputs);
            Assert.Equal(outputs, generated.Where(number => number <= 21));
        }

        public static TheoryData<string> FunctionContracts => new TheoryData<string>(FunctionPrograms.Texts.Keys);

        // Laid out as evolved parts are, so the contract's own binding reads it.
        [Theory]
        [MemberData(nameof(FunctionContracts))]
        public void ACompiledFunctionProgramMeetsItsContract(string name)
        {
            Contract contract = FirstParts.Named(name);
            Network network = RegisterMachineCompiler.Compile(FunctionPrograms.For(name));

            Assert.Equal("meets the contract", Runs.Evaluate(new ContractTask(contract), network).Description);
        }
    }
}
