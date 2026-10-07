using SnpEvolution.Search.Benchmarking;
using SnpEvolution.Search.Fitness;
using static SnpEvolution.Tests.Fixtures.Runs;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Search.Benchmarking
{
    public class TaskSuiteTests
    {
        [Fact]
        public void SuiteTasksAreAllUsableWithTheirOwnSettings()
        {
            foreach (BenchmarkTask task in TaskSuite.All)
            {
                Assert.NotEmpty(task.Task.Cases);
                FitnessResult result = Runs.Evaluate(task.Task, Identity(), options: TaskOptions, solvedRetestCount: 3);
                Assert.InRange(result.Fitness, 0, 1);
            }
        }
    }
}
