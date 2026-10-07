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

        [Fact]
        public void ProgramSearchLearnsLargeSetsInStagesThatEndPastTheLargestNumber()
        {
            Assert.Equal(new long[] { 5, 13, 34, 89, 233, 610, 1976 }, ProgramScoring.StageBounds(new[] { 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144, 233, 377, 610, 987 }));
            Assert.Equal(new long[] { 14 }, ProgramScoring.StageBounds(new[] { 2, 4, 6 }));
        }

        [Fact]
        public void AStageChecksEveryTargetNumberUpToItsBoundAndNothingExtra()
        {
            var scoring = new ProgramScoring(new[] { 1, 2, 3, 5, 8 }, 2_000, new EvaluationBudget());

            ScoredProgram scored = scoring.Score(RegisterProgram.Parse("ADD r0 -> 1\nADD r0 -> 0 | 2\nHALT"));

            Assert.Equal(5, scored.Checks.Count);
        }

        [Fact]
        public void AnEditNeverLeavesAProgramWithoutInstructions()
        {
            var edits = new ProgramEdits(16, 4, new Random(1));
            RegisterProgram single = RegisterProgram.Parse("HALT");

            Assert.All(Enumerable.Range(0, 300), _ => Assert.NotEmpty(edits.Mutate(single).Instructions));
        }
    }
}
