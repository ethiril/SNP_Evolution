using SnpEvolution.Search.Benchmarking;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Tasks;
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

        [Fact]
        public void BothStreamingTasksAreInTheSuite()
        {
            List<string> names = TaskSuite.All.Select(task => task.Name).ToList();

            Assert.Contains(StreamingTask.Debouncer(spikes: 2, within: 3).Name, names);
            Assert.Contains(StreamingTask.RateDetector(spikes: 3, within: 6).Name, names);
        }

        [Fact]
        public void EveryArithmeticContractIsATaskInTheSuite()
        {
            List<string> names = TaskSuite.All.Select(task => task.Name).ToList();

            Assert.All(ArithmeticParts.Contracts, contract => Assert.Contains("Contract " + contract.Name, names));
        }
    }
}
