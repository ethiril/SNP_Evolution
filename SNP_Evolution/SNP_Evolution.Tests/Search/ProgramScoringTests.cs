using SnpEvolution.Compilation;
using SnpEvolution.Search;
using SnpEvolution.Specs.Accounting;

namespace SnpEvolution.Tests.Search
{
    public class ProgramScoringTests
    {
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
    }
}
