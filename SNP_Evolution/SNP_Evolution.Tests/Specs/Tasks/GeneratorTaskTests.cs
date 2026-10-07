using SnpEvolution.Search.Fitness;
using SnpEvolution.Specs.Tasks;
using static SnpEvolution.Tests.Fixtures.Runs;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Specs.Tasks
{
    public class GeneratorTaskTests
    {
        [Fact]
        public void GeneratorTaskScoresWithItsFitnessFunction()
        {
            var task = new GeneratorTask("{1}", new[] { 1 }, new JaccardFitness(new[] { 1 }));

            FitnessResult result = Runs.Evaluate(task, AlwaysOutputsOne(), options: TaskOptions, solvedRetestCount: 3);

            Assert.Equal(1f, result.Fitness);
            Assert.Equal("{1}", result.Description);
            Assert.Equal(new[] { 0f, 1f, 0f }, new GeneratorTask("{1,2,3}", new[] { 1, 2, 3 }, new JaccardFitness(new[] { 1, 2, 3 })).Checks(new[] { 2 }));
        }
    }
}
