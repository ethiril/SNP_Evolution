using SnpEvolution.Model;
using SnpEvolution.Search;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Search.Genome;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Tests.Specs.Tasks
{
    public class ContractTaskTests
    {
        private static FitnessResult Verify(Part part) => Runs.Evaluate(part.Task(), part.Network);

        private static IEnumerable<float> RuleChecks(FitnessResult result, ContractRule rule) =>
            result.Checks!.Where((_, check) => check % ContractTask.RuleCount == (int)rule);

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
            FitnessResult result = Verify(PartFixtures.Register(8));

            Assert.True(result.Exact);
            Assert.Equal(9 * ContractTask.RuleCount, result.Checks!.Count);
            Assert.Equal(1f, result.Fitness);
        }

        [Theory]
        [MemberData(nameof(Delays))]
        public void ADelayFiringDoneTwiceFailsOnlyDoneOnce(int k)
        {
            FitnessResult result = Verify(PartFixtures.DelayFiringDoneTwice(k));

            Assert.True(result.Exact);
            Assert.All(RuleChecks(result, ContractRule.DoneOnce), score => Assert.Equal(0f, score));
            Assert.All(new[] { ContractRule.QuietBeforeStart, ContractRule.BackToStart, ContractRule.OnTime }.SelectMany(rule => RuleChecks(result, rule)), score => Assert.Equal(1f, score));
        }

        [Fact]
        public void ARegisterLeavingASpikeFailsOnlyBackToStart()
        {
            FitnessResult result = Verify(PartFixtures.RegisterLeavingASpike(8));

            Assert.True(result.Exact);
            Assert.All(RuleChecks(result, ContractRule.BackToStart), score => Assert.True(score < 1));
            Assert.All(new[] { ContractRule.QuietBeforeStart, ContractRule.DoneOnce, ContractRule.OnTime }.SelectMany(rule => RuleChecks(result, rule)), score => Assert.Equal(1f, score));
        }

        [Fact]
        public void ADelayThatIsTooQuickFailsOnlyOnTime()
        {
            FitnessResult result = Runs.Evaluate(new ContractTask(PartFixtures.DelayContract(3)), ReferenceParts.Delay(2).Network);

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

            FitnessResult result = Runs.Evaluate(PartFixtures.Register(3).Task(), silent);

            Assert.All(RuleChecks(result, ContractRule.QuietBeforeStart), score => Assert.Equal(1f, score));
            Assert.All(RuleChecks(result, ContractRule.DoneOnce), score => Assert.Equal(0f, score));
            Assert.All(RuleChecks(result, ContractRule.OnTime), score => Assert.Equal(0f, score));
        }

        // The output fires once more than it should: close, so it earns part of the done-once check.
        [Fact]
        public void ACountThatIsCloseEarnsPartialCredit()
        {
            Part register = PartFixtures.Register(3);
            Contract offByOne = register.Contract with
            {
                Cases = register.Contract.Cases
                    .Select(@case => @case with { Outputs = new Dictionary<string, int> { ["out"] = @case.Outputs["out"] + 1 } })
                    .ToList(),
            };

            FitnessResult result = Runs.Evaluate(new ContractTask(offByOne, register.Binding), register.Network);

            Assert.All(RuleChecks(result, ContractRule.DoneOnce), score => Assert.InRange(score, 0.1f, 0.9f));
            Assert.InRange(result.Fitness, 0.5f, 0.99f);
        }

        // Start is sent on step 2, so it reaches the part on step 3; done fires on step 10, and the binary word follows it.
        private static readonly Contract IntervalTriggerAndWord = new Contract(
            "outputs",
            Port.In("start", PortKind.Trigger),
            new[] { Port.Out("done", PortKind.Trigger) },
            new[] { Port.Out("gap", PortKind.Interval), Port.Out("flag", PortKind.Trigger), Port.Out("word", PortKind.Binary, 3) },
            new[] { new ContractCase(new Dictionary<string, int>(), new Dictionary<string, int> { ["gap"] = 3, ["flag"] = 1, ["word"] = 5 }, "done") },
            MaxLatency: 8);

        private static IReadOnlyList<float> ChecksFor(int[] gap, int[] flag, int[] word)
        {
            Firing[] At(int[] steps) => steps.Select(step => new Firing(step, 1)).ToArray();
            var run = new PortRun(new[] { At(gap), At(flag), At(word), At(new[] { 10 }) }, new long[5], new long[5]);
            return new ContractTask(IntervalTriggerAndWord).Checks(new[] { new TrialResult(Array.Empty<int>(), false, TrialCoverage.Exact, PortRuns: new[] { run }) });
        }

        [Fact]
        public void ReadsIntervalTriggerAndBinaryOutputs() =>
            Assert.Equal(new[] { 1f, 1f, 1f, 1f }, ChecksFor(gap: new[] { 4, 7 }, flag: new[] { 5 }, word: new[] { 10, 12 }));

        // Start is sent on step 2, so a firing then is no part of the gap.
        [Fact]
        public void AnOutputFiringOnTheStartStepIsNotPartOfItsValue() =>
            Assert.Equal(2 / 3f, ChecksFor(gap: new[] { 2, 5 }, flag: new[] { 5 }, word: new[] { 10, 12 })[(int)ContractRule.DoneOnce], 4);

        // The gap is one too long (0.25), the flag fires twice (0) and the word has one wrong bit of three (1/3).
        [Fact]
        public void ScoresWrongIntervalTriggerAndBinaryOutputs() =>
            Assert.Equal((0.25f + 0 + 1 / 3f) / 3, ChecksFor(gap: new[] { 4, 8 }, flag: new[] { 5, 6 }, word: new[] { 10 })[(int)ContractRule.DoneOnce], 4);

        [Fact]
        public void AFiringOnTheStartStepIsNotQuiet() =>
            Assert.Equal(0f, ChecksFor(gap: new[] { 2, 5 }, flag: new[] { 5 }, word: new[] { 10, 12 })[(int)ContractRule.QuietBeforeStart]);

        [Fact]
        public void ATriggerOnTheDoneStepIsLate() =>
            Assert.Equal(2 / 3f, ChecksFor(gap: new[] { 4, 7 }, flag: new[] { 10 }, word: new[] { 10, 12 })[(int)ContractRule.DoneOnce], 4);

        [Fact]
        public void ABinaryWordBeforeDoneIsWrong() =>
            Assert.Equal(2 / 3f, ChecksFor(gap: new[] { 4, 7 }, flag: new[] { 5 }, word: new[] { 9, 12 })[(int)ContractRule.DoneOnce], 4);

        [Fact]
        public void ChecksAreNamedByCaseAndRule()
        {
            ContractTask task = PartFixtures.Register().Task();

            Assert.Equal("n=0: quiet before start", task.CheckName(0));
            Assert.Equal("n=3: done once", task.CheckName(ContractTask.CheckIndex(3, ContractRule.DoneOnce)));
            Assert.Equal("n=8: on time", task.CheckName(ContractTask.CheckIndex(8, ContractRule.OnTime)));
            Assert.Equal("case 1: back to start", ReferenceParts.Delay(2).Task().CheckName(ContractTask.CheckIndex(0, ContractRule.BackToStart)));
        }

        [Fact]
        public void DescribeListsEachFailingCheckOnItsOwnLine()
        {
            FitnessResult result = Verify(PartFixtures.DelayFiringDoneTwice(2));

            Assert.Equal("case 1: done once 0", result.Description);
        }

        [Fact]
        public void TheNicheIsSizeByLatency()
        {
            FitnessResult result = Verify(PartFixtures.Register(4));

            Assert.Equal((5, 6), result.Niche);
        }

        [Fact]
        public void InputsAreStartThenEachDataInPortAndOutputsFollowTheInputs()
        {
            ContractTask task = PartFixtures.Register().Task();

            Assert.Equal(2, task.InputCount);
            Assert.Equal(3, task.Binding["out"]);
            Assert.Equal(4, task.Binding["done"]);
            Assert.Equal(new[] { 3, 4 }, task.Cases[0].Watch!.Neurons);
            Assert.Equal(new[] { 4 }, task.Cases[0].Watch!.Done);
        }

        [Fact]
        public void ABindingMustGiveEveryOutPortItsOwnNeuron()
        {
            Contract register = PartFixtures.RegisterContract();
            var shared = new PortBinding(new Dictionary<string, int> { ["out"] = 3, ["done"] = 3 });
            var missing = new PortBinding(new Dictionary<string, int> { ["out"] = 3 });
            var zero = new PortBinding(new Dictionary<string, int> { ["out"] = 0, ["done"] = 4 });

            Assert.Contains("share neuron 3", Assert.Throws<ArgumentException>(() => new ContractTask(register, shared)).Message);
            Assert.Contains("Port 'done' has no neuron", Assert.Throws<ArgumentException>(() => new ContractTask(register, missing)).Message);
            Assert.Contains("positions start at 1", Assert.Throws<ArgumentException>(() => new ContractTask(register, zero)).Message);
        }

        public static TheoryData<string> Algorithms => new TheoryData<string> { "Generational, tournament of 3, structural", "MAP-Elites over network size" };

        [Theory]
        [MemberData(nameof(Algorithms))]
        public void RunsUnderTheExistingAlgorithms(string name)
        {
            var random = new Random(2);
            ContractTask task = ReferenceParts.Delay(2).Task();
            var factory = Factories.Networks(new GenomeSpace(InputCount: task.InputCount, RuleForm: RuleForm.Standard, MinNeurons: task.Binding.NeuronsNeeded), random, ExpressionGenerator.SimpleTemplates);
            var evaluator = new FitnessEvaluator(new ExhaustiveCpuEngine(), task, new SimulationOptions(1, 5, OutputTiming.Interval), 1, random, new EvaluationBudget());
            IGeneticAlgorithm algorithm = SearchCatalog.Evolution.Single(search => search.Name == name)
                .Create(new EvolutionContext(12, 0.5f, random, factory.NewNetwork, evaluator, factory, _ => { }));

            for (int generation = 0; generation < 5; generation++)
            {
                algorithm.NextGeneration();
            }

            Assert.InRange(algorithm.Best!.Fitness, 0f, 1f);
            Assert.NotEmpty(algorithm.Population);
        }

        [Fact]
        public void FailingCasesProposeASubContract()
        {
            var task = new ContractTask(FirstParts.Named("increment"));
            int caseThree = FirstParts.Values.ToList().IndexOf(3);

            Contract sub = task.Propose(new[] { ContractTask.CheckIndex(caseThree, ContractRule.DoneOnce) })!;
            Assert.Equal("increment on n=3", sub.Name);
            Assert.Equal(4, sub.Cases.Single().Outputs["out"]);
            Assert.Null(task.Propose(Enumerable.Range(0, task.Contract.Cases.Count).Select(caseIndex => ContractTask.CheckIndex(caseIndex, ContractRule.OnTime)).ToList()));
        }
    }
}
