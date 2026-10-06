using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using static SnpEvolution.Tests.TestNetworks;

namespace SnpEvolution.Tests.Evolution
{
    public class TaskTests
    {
        private static readonly SimulationOptions Options = new SimulationOptions(MaxSteps: 40, Repetitions: 20, OutputTiming.Interval);

        private static FitnessResult Evaluate(ITask task, Network network) =>
            new FitnessEvaluator(new ExhaustiveCpuEngine(), task, Options, solvedRetestCount: 3, new Random(0)).Evaluate(network);

        private static TrialResult Outputs(params int[] outputs) => new TrialResult(outputs, false, TrialCoverage.Exact);

        private static TrialResult Halts(bool halts) => new TrialResult(Array.Empty<int>(), halts, TrialCoverage.Exact);

        [Fact]
        public void IdentityNetworkSolvesTheIdentityFunctionExactly()
        {
            FitnessResult result = Evaluate(FunctionTask.Of("n", n => n, Enumerable.Range(1, 6)), Identity());

            Assert.Equal(1f, result.Fitness);
            Assert.True(result.Exact);
            Assert.Contains("f(3)={3}/3", result.Description);
        }

        [Fact]
        public void IdentityNetworkIsCloseButWrongForTheSuccessor()
        {
            float fitness = Evaluate(FunctionTask.Of("n + 1", n => n + 1, Enumerable.Range(1, 6)), Identity()).Fitness;

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

        [Fact]
        public void AcceptorScoreIsBalancedAccuracy()
        {
            AcceptorTask task = AcceptorTask.Of("even", n => n % 2 == 0, new[] { 1, 2, 3, 4, 5, 6 });

            Assert.Equal(1f, task.Score(new[] { false, true, false, true, false, true }.Select(Halts).ToList()));
            Assert.Equal(0.5f, task.Score(Enumerable.Repeat(Halts(true), 6).ToList()));
            Assert.Equal(0.5f, task.Score(Enumerable.Repeat(Halts(false), 6).ToList()));
            Assert.Equal(Readout.Halting, task.Cases[0].Readout);
        }

        [Fact]
        public void AcceptorThatAlwaysHaltsAcceptsEverything()
        {
            var network = new Network(new[] { InputNeuron(Array.Empty<int>(), StandardForget("a", 1)), OutputNeuron(0, Standard("a", 1)) });

            FitnessResult result = Evaluate(AcceptorTask.Of("big", n => n >= 3, Enumerable.Range(1, 4)), network);

            Assert.Equal(0.5f, result.Fitness);
            Assert.Equal("accepts {1,2,3,4}", result.Description);
        }

        [Fact]
        public void GeneratorTaskScoresWithItsFitnessFunction()
        {
            var task = new GeneratorTask("{1}", new[] { 1 }, new JaccardFitness(new[] { 1 }));

            FitnessResult result = Evaluate(task, AlwaysOutputsOne());

            Assert.Equal(1f, result.Fitness);
            Assert.Equal("{1}", result.Description);
        }

        [Fact]
        public void ExactResultIsReliablySolvedWithoutRetesting()
        {
            var evaluator = new FitnessEvaluator(new ExhaustiveCpuEngine(), FunctionTask.Of("n", n => n, new[] { 1, 2 }), Options, solvedRetestCount: 5, new Random(0));

            Assert.True(evaluator.IsReliablySolved(Identity()));
            Assert.Equal(1, evaluator.Evaluations);
        }

        [Fact]
        public void SuiteTasksAreAllUsableWithTheirOwnSettings()
        {
            foreach (BenchmarkTask task in TaskSuite.All)
            {
                Assert.NotEmpty(task.Task.Cases);
                FitnessResult result = Evaluate(task.Task, Identity());
                Assert.InRange(result.Fitness, 0, 1);
            }
        }
    }
}
