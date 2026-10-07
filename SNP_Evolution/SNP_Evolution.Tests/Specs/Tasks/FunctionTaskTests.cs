using SnpEvolution.Search.Fitness;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Tasks;
using static SnpEvolution.Tests.Fixtures.Runs;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Specs.Tasks
{
    public class FunctionTaskTests
    {
        private static TrialResult Outputs(params int[] outputs) => new TrialResult(outputs, false, TrialCoverage.Exact);

        [Fact]
        public void IdentityNetworkSolvesTheIdentityFunctionExactly()
        {
            FitnessResult result = Runs.Evaluate(FunctionTask.Of("n", n => n, Enumerable.Range(1, 6)), Identity(), options: TaskOptions, solvedRetestCount: 3);

            Assert.Equal(1f, result.Fitness);
            Assert.True(result.Exact);
            Assert.Contains("f(3)={3}/3", result.Description);
        }

        [Fact]
        public void IdentityNetworkIsCloseButWrongForTheSuccessor()
        {
            float fitness = Runs.Evaluate(FunctionTask.Of("n + 1", n => n + 1, Enumerable.Range(1, 6)), Identity(), options: TaskOptions, solvedRetestCount: 3).Fitness;

            Assert.Equal(0.25f, fitness, precision: 5);
        }

        [Fact]
        public void FunctionScoreGivesFullCreditOnlyForTheRightOutput()
        {
            var task = new FunctionTask("f", new[] { new FunctionExample(new[] { 2 }, 4), new FunctionExample(new[] { 3 }, 6) });

            Assert.Equal(1f, task.Score(new[] { Outputs(4), Outputs(6) }));
            Assert.Equal(0.5f, task.Score(new[] { Outputs(4), Outputs() }));
            Assert.Equal(0.78125f, task.Score(new[] { Outputs(4, 4), Outputs(6, 9) }), precision: 5);
        }

        [Fact]
        public void TwoArgumentFunctionsNeedTwoInputs()
        {
            FunctionTask task = FunctionTask.Of("add", (first, second) => first + second, new[] { (1, 2) });

            Assert.Equal(2, task.InputCount);
            Assert.Equal(new[] { 0, 2 }, task.Cases[0].Input.StepsPerInput[1]);
        }
    }
}
