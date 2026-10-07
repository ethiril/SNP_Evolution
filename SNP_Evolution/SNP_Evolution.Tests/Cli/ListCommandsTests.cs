using SnpEvolution.Search.Benchmarking;

namespace SnpEvolution.Tests.Cli
{
    [Collection(ProcessStateCollection.Name)]
    public class ListCommandsTests
    {
        [Fact]
        public void TasksListsEverySuiteTask()
        {
            using var run = new CommandRun();

            string output = run.Run("tasks");

            Assert.All(TaskSuite.All, task => Assert.Contains(task.Name, output));
        }
    }
}
