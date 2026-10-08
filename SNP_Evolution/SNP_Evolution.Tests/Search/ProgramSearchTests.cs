using SnpEvolution.Compilation;
using SnpEvolution.Search;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Tasks;
using static SnpEvolution.Tests.Fixtures.Runs;

namespace SnpEvolution.Tests.Search
{
    public class ProgramSearchTests
    {
        [Fact]
        public void ProgramSearchFindsASmallSetAndItsNetworkGeneratesIt()
        {
            var search = new ProgramSearch(new ProgramSearchSettings(Population: 60));
            var target = new GeneratorTask("{2,3}", new[] { 2, 3 }, new JaccardFitness(new[] { 2, 3 }));
            var budget = new EvaluationBudget();

            SearchOutcome<RegisterProgram> best = search.Run(new SearchRequest<RegisterProgram>(target, budget, new Random(1), _ => { }) { MaxGenerations = 400 });

            Assert.True(best.Solved, best.Best!.ToString());
            Assert.Matches(@"\(stage (\d+)/\1\)$", best.Description);
            Assert.Equal(new[] { 2, 3 }, Generated(RegisterMachineCompiler.Compile(best.Best!)));
            Assert.True(budget[EvaluationKind.InterpreterRun] > 0);
        }

        // Searched against the contract's cases, the program computes every case in the interpreter, ending on the right done.
        [Theory]
        [InlineData("register")]
        [InlineData("zero test")]
        public void ProgramSearchFindsAFunctionProgramForACountContract(string name)
        {
            Contract contract = FirstParts.Named(name);
            var budget = new EvaluationBudget();

            SearchOutcome<RegisterProgram> best = new ProgramSearch().Run(new SearchRequest<RegisterProgram>(new ContractTask(contract), budget, new Random(1), _ => { }) { MaxGenerations = 500 });

            Assert.True(best.Solved, best.Description);
            FunctionProgram program = FunctionScoring.Layout(contract, best.Best!);
            Assert.All(contract.Cases, @case => Assert.All(program.Run(@case.Inputs, FunctionScoring.StepsFor(@case)).Outcomes, outcome => Assert.Equal(@case.Done, outcome.Done)));
        }

        // Every contract whose data ports are all counts is a target, the branching compare and zero test among them.
        [Fact]
        public void EveryCountContractIsATargetForARegisterProgram()
        {
            Assert.Equal(new[] { "fan-out", "increment", "double", "add", "register", "zero test", "subtract", "multiply", "divide", "compare" },
                FirstParts.Contracts.Concat(ArithmeticParts.Count).Where(contract => contract.DataIn.Any() && FunctionScoring.Fits(contract)).Select(contract => contract.Name));
            Assert.False(FunctionScoring.Fits(FirstParts.Named("count to interval")));
        }
    }
}
