using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Tests.Evolution
{
    public class ContractTaskTests
    {
        private const int Quiet = 0, DoneOnce = 1, BackToStart = 2, OnTime = 3;

        private static FitnessResult Verify(ContractTask task, Network network) =>
            new FitnessEvaluator(new ExhaustiveCpuEngine(), task, new SimulationOptions(1, 5, OutputTiming.Interval), 1, new Random(0)).Evaluate(network);

        private static FitnessResult Verify(Part part) => Verify(part.Task(), part.Network);

        // Each check of the given rule, one per case.
        private static IEnumerable<float> RuleChecks(FitnessResult result, int rule) =>
            result.Checks!.Where((_, check) => check % ContractTask.RuleCount == rule);

        public static TheoryData<int> Delays => new TheoryData<int> { 1, 2, 3, 4 };

        [Theory]
        [MemberData(nameof(Delays))]
        public void TheHandBuiltDelayMeetsItsContract(int k)
        {
            FitnessResult result = Verify(ReferenceParts.Delay(k));

            Assert.True(result.Exact);
            Assert.Equal(1f, result.Fitness);
            Assert.Equal("meets the contract", result.Description);
        }

        [Fact]
        public void TheHandBuiltRegisterMeetsItsContractForZeroToEight()
        {
            FitnessResult result = Verify(ReferenceParts.Register(largest: 8));

            Assert.True(result.Exact);
            Assert.Equal(9 * ContractTask.RuleCount, result.Checks!.Count);
            Assert.Equal(1f, result.Fitness);
        }

        [Theory]
        [MemberData(nameof(Delays))]
        public void ADelayFiringDoneTwiceFailsOnlyDoneOnce(int k)
        {
            FitnessResult result = Verify(ReferenceParts.DelayFiringDoneTwice(k));

            Assert.True(result.Exact);
            Assert.All(RuleChecks(result, DoneOnce), score => Assert.Equal(0f, score));
            Assert.All(new[] { Quiet, BackToStart, OnTime }.SelectMany(rule => RuleChecks(result, rule)), score => Assert.Equal(1f, score));
        }

        [Fact]
        public void ARegisterLeavingASpikeFailsOnlyBackToStart()
        {
            FitnessResult result = Verify(ReferenceParts.RegisterLeavingASpike(largest: 8));

            Assert.True(result.Exact);
            Assert.All(RuleChecks(result, BackToStart), score => Assert.True(score < 1));
            Assert.All(new[] { Quiet, DoneOnce, OnTime }.SelectMany(rule => RuleChecks(result, rule)), score => Assert.Equal(1f, score));
        }

        [Fact]
        public void ADelayThatIsTooQuickFailsOnlyOnTime()
        {
            FitnessResult result = Verify(new ContractTask(ReferenceParts.DelayContract(3)), ReferenceParts.Delay(2).Network);

            Assert.Equal(new[] { 1f, 1f, 1f, 0f }, result.Checks);
        }

        [Fact]
        public void ANetworkThatNeverFiresDoneFailsDoneOnceAndOnTimeButIsQuiet()
        {
            var silent = new Network(new[]
            {
                new Neuron(Array.Empty<Rule>(), 0, new[] { 3 }, false, isInput: true),
                new Neuron(Array.Empty<Rule>(), 0, Array.Empty<int>(), false, isInput: true),
                new Neuron(Array.Empty<Rule>(), 0, Array.Empty<int>(), false),
                new Neuron(Array.Empty<Rule>(), 0, Array.Empty<int>(), false),
            });

            FitnessResult result = Verify(ReferenceParts.Register(largest: 3).Task(), silent);

            Assert.All(RuleChecks(result, Quiet), score => Assert.Equal(1f, score));
            Assert.All(RuleChecks(result, DoneOnce), score => Assert.Equal(0f, score));
            Assert.All(RuleChecks(result, OnTime), score => Assert.Equal(0f, score));
        }

        // The output fires once more than it should: close, so it earns part of the done-once check.
        [Fact]
        public void ACountThatIsCloseEarnsPartialCredit()
        {
            Part register = ReferenceParts.Register(largest: 3);
            Contract offByOne = register.Contract with
            {
                Cases = register.Contract.Cases
                    .Select(@case => @case with { Outputs = new Dictionary<string, int> { ["out"] = @case.Outputs["out"] + 1 } })
                    .ToList(),
            };

            FitnessResult result = Verify(new ContractTask(offByOne, register.Binding), register.Network);

            Assert.All(RuleChecks(result, DoneOnce), score => Assert.InRange(score, 0.1f, 0.9f));
            Assert.InRange(result.Fitness, 0.5f, 0.99f);
        }

        [Fact]
        public void ChecksAreNamedByCaseAndRule()
        {
            ContractTask task = ReferenceParts.Register().Task();

            Assert.Equal("n=0: quiet before start", task.CheckName(0));
            Assert.Equal("n=3: done once", task.CheckName(3 * ContractTask.RuleCount + DoneOnce));
            Assert.Equal("n=8: on time", task.CheckName(8 * ContractTask.RuleCount + OnTime));
            Assert.Equal("case 1: back to start", ReferenceParts.Delay(2).Task().CheckName(BackToStart));
        }

        [Fact]
        public void DescribeListsEachFailingCheckOnItsOwnLine()
        {
            FitnessResult result = Verify(ReferenceParts.DelayFiringDoneTwice(2));

            Assert.Equal("case 1: done once 0", result.Description);
        }

        [Fact]
        public void TheNicheIsSizeByLatency()
        {
            FitnessResult result = Verify(ReferenceParts.Register(largest: 4));

            Assert.Equal((5, 6), result.Niche);
        }

        [Fact]
        public void InputsAreStartThenEachDataInPortAndOutputsFollowTheInputs()
        {
            ContractTask task = ReferenceParts.Register().Task();

            Assert.Equal(2, task.InputCount);
            Assert.Equal(3, task.Binding["out"]);
            Assert.Equal(4, task.Binding["done"]);
            Assert.Equal(new[] { 3, 4 }, task.Cases[0].Watch!.Neurons);
            Assert.Equal(new[] { 4 }, task.Cases[0].Watch!.Done);
        }

        [Fact]
        public void ABindingMustGiveEveryOutPortItsOwnNeuron()
        {
            Contract register = ReferenceParts.RegisterContract();
            var shared = new PortBinding(new Dictionary<string, int> { ["out"] = 3, ["done"] = 3 });
            var missing = new PortBinding(new Dictionary<string, int> { ["out"] = 3 });

            Assert.Contains("share neuron 3", Assert.Throws<ArgumentException>(() => new ContractTask(register, shared)).Message);
            Assert.Contains("Port 'done' has no neuron", Assert.Throws<ArgumentException>(() => new ContractTask(register, missing)).Message);
        }

        public static TheoryData<string> Algorithms => new TheoryData<string> { "Generational, tournament of 3, structural", "MAP-Elites over network size" };

        [Theory]
        [MemberData(nameof(Algorithms))]
        public void RunsUnderTheExistingAlgorithms(string name)
        {
            var random = new Random(2);
            ContractTask task = ReferenceParts.Delay(2).Task();
            var factory = new NetworkFactory(
                new GenomeSpace(InputCount: task.InputCount, RuleForm: RuleForm.Standard, MinNeurons: task.Binding.NeuronsNeeded),
                new ExpressionGenerator(ExpressionGenerator.SimpleTemplates, 4, random),
                random);
            var evaluator = new FitnessEvaluator(new ExhaustiveCpuEngine(), task, new SimulationOptions(1, 5, OutputTiming.Interval), 1, random);
            IGeneticAlgorithm algorithm = AlgorithmCatalog.All.Single(choice => choice.Name == name)
                .Create(new EvolutionContext(12, 0.5f, random, factory.NewNetwork, evaluator, factory, _ => { }));

            for (int generation = 0; generation < 5; generation++)
            {
                algorithm.NextGeneration();
            }

            Assert.InRange(algorithm.Best!.Fitness, 0f, 1f);
            Assert.NotEmpty(algorithm.Population);
        }
    }
}
