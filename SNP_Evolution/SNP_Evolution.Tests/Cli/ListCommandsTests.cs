using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Tests.Golden;

namespace SnpEvolution.Tests.Cli
{
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
