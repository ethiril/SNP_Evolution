using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Tests.Specs.Tasks
{
    public class CurriculumPlanTests
    {
        [Fact]
        public void CurriculumStagesEndWithTheWholeTarget()
        {
            Assert.Equal(new[] { 3, 4, 5, 6 }, new CurriculumPlan(3, 1).Lengths(6));
            Assert.Equal(new[] { 8, 12, 15 }, new CurriculumPlan(8, 4).Lengths(15));
            Assert.Equal(new[] { 4 }, new CurriculumPlan(10, 2).Lengths(4));
        }

        [Fact]
        public void BinaryWordsStartWithLongerStagesThanSequences()
        {
            Assert.Equal(new CurriculumPlan(8, 4), ((IPrefixTask)new SpikeWordTask("word", Enumerable.Repeat(true, 20).ToList())).Curriculum);
            Assert.Equal(new CurriculumPlan(3, 1), ((IPrefixTask)new SequenceTask("gaps", new[] { 1, 2, 3, 4, 5 })).Curriculum);
        }
    }
}
