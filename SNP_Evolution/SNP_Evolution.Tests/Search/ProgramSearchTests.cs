using SnpEvolution.Compilation;
using SnpEvolution.Search;
using SnpEvolution.Specs.Accounting;
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
            Assert.Equal(new[] { 2, 3 }, Generated(RegisterMachineCompiler.Compile(best.Best!)));
            Assert.True(budget[EvaluationKind.InterpreterRun] > 0);
        }
    }
}
