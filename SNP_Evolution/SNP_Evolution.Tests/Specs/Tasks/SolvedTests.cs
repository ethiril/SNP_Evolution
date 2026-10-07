using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Tests.Specs.Tasks
{
    public class SolvedTests
    {
        [Theory]
        [InlineData(0.985f, true)]
        [InlineData(1f, true)]
        [InlineData(0.984f, false)]
        [InlineData(1.01f, false)]
        [InlineData(float.NaN, false)]
        public void SolvingFitnessIsBetweenTheThresholdAndOne(float fitness, bool solving)
        {
            Assert.Equal(solving, Solved.Solves(fitness));
        }
    }
}
