using SnpEvolution.Model;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;
using SnpEvolution.Storage;
using SnpEvolution.Tests.Fixtures;

namespace SnpEvolution.Tests.Specs.Contracts
{
    public class FirstPartsTests
    {
        private const string ExpectedTable =
            "| Part | Ports besides start and done | Contract |\n" +
            "|---|---|---|\n" +
            "| Delay k | none | done fires k steps after start (k = 1..4, one contract each) |\n" +
            "| Fan-out | count in; count out x2 | both outputs carry n |\n" +
            "| Increment | count in; count out | output carries n + 1 |\n" +
            "| Double | count in; count out | output carries 2n |\n" +
            "| Add | count in x2; count out | output carries n1 + n2 (cases cover pairs up to 6 + 6) |\n" +
            "| Interval to count | interval in; count out | output carries n |\n" +
            "| Count to interval (timer) | count in; interval out | two output spikes n steps apart |\n" +
            "| Register | count in; count out | holds n until started again, then drains it |\n" +
            "| Zero test | count in; done-zero, done-nonzero | the right branch fires, the other never |\n" +
            "| Sequencer | done out xk | fires its outputs in order, each one step after the previous (k = 2, 3) |\n" +
            "| Join | trigger in x2 | done fires once after both inputs, in either order and up to 4 steps apart |\n" +
            "| Fork | trigger out x2 | both outputs fire on one step, then done |\n" +
            "| Merge | trigger in x2 | done fires once after whichever input fired (never both) |\n" +
            "| Select | trigger in; done-one, done-zero | done-one when the input fired with start, done-zero when it did not |\n";

        public static TheoryData<string> ContractNames => new TheoryData<string>(FirstParts.Contracts.Select(contract => contract.Name));

        [Fact]
        public void FourteenPartsMakeEighteenContracts()
        {
            Assert.Equal(14, FirstParts.All.Count);
            Assert.Equal(18, FirstParts.Contracts.Count);
            Assert.Equal(FirstParts.Contracts.Count, FirstParts.Contracts.Select(contract => contract.Name).Distinct().Count());
        }

        [Theory]
        [MemberData(nameof(ContractNames))]
        public void EveryContractValidates(string name) => Assert.Empty(FirstParts.Named(name).Problems());

        [Theory]
        [MemberData(nameof(ContractNames))]
        public void EveryContractRoundTripsThroughJson(string name)
        {
            Contract contract = FirstParts.Named(name);

            Assert.Equal(Json.Write(contract), Json.Write(Json.Read<Contract>(Json.Write(contract))!));
        }

        [Fact]
        public void TheCatalogueTableMatchesTheGoalsGiven()
        {
            Assert.Equal(ExpectedTable, FirstParts.Table());
        }

        [Theory]
        [InlineData("fan-out")]
        [InlineData("increment")]
        [InlineData("register")]
        [InlineData("zero test")]
        public void CountCasesRunFromZeroToEightPlusALargerValue(string name) =>
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 12 }, FirstParts.Named(name).Cases.Select(@case => @case.Inputs["n"]));

        [Fact]
        public void AddCoversEveryPairUpToSixPlusSix()
        {
            var pairs = FirstParts.Named("add").Cases.Select(@case => (@case.Inputs["a"], @case.Inputs["b"])).ToHashSet();

            Assert.All(Enumerable.Range(0, 7).SelectMany(a => Enumerable.Range(0, 7).Select(b => (a, b))), pair => Assert.Contains(pair, pairs));
            Assert.Contains(pairs, pair => pair.Item1 > 6);
            Assert.Contains(pairs, pair => pair.Item2 > 6);
        }

        // Every hand-built part passes the verifier; this pins the wording the scorer gives a pass.
        [Theory]
        [InlineData("register")]
        [InlineData("add")]
        public void AHandBuiltPartIsDescribedAsMeetingTheContract(string name)
        {
            Part part = HandBuiltParts.All().Single(candidate => candidate.Contract.Name == name);

            Assert.Equal("meets the contract", Runs.Evaluate(part.Task(), part.Network).Description);
        }

        // start -> t1 -> t2 -> done, a relay chain; listed the other way round, t2 fires first.
        private static Network SequencerChain(bool swapped) => new Network(new[]
        {
            new Neuron(new[] { Rule.Standard("a", 1) }, 0, new[] { swapped ? 3 : 2 }, false, isInput: true),
            new Neuron(new[] { Rule.Standard("a", 1) }, 0, new[] { swapped ? 4 : 3 }, false),
            new Neuron(new[] { Rule.Standard("a", 1) }, 0, new[] { swapped ? 2 : 4 }, false),
            new Neuron(new[] { Rule.Standard("a", 1) }, 0, new int[0], false),
        });

        [Fact]
        public void ARelayChainMeetsTheSequencerContract()
        {
            Assert.Equal("meets the contract", Runs.Evaluate(new ContractTask(FirstParts.Named("sequencer 2")), SequencerChain(swapped: false)).Description);
        }

        [Fact]
        public void OutputsFiringOutOfOrderFailOnlyValues()
        {
            FitnessResult result = Runs.Evaluate(new ContractTask(FirstParts.Named("sequencer 2")), SequencerChain(swapped: true));

            Assert.Equal(new[] { 1f, 1f, 0f, 1f, 1f }, result.Checks);
        }

        [Fact]
        public void ANeuronFiringOnAPairMeetsTheJoinContract()
        {
            Assert.Equal("meets the contract", Runs.Evaluate(new ContractTask(FirstParts.Named("join")), ControlNetworks.JoinOnAPair(relay: false)).Description);
        }

        // A relay fires as soon as the first input arrives, and again for the second.
        [Fact]
        public void ARelayFailsTheJoinWhenItsInputsComeApart()
        {
            var task = new ContractTask(FirstParts.Named("join"));
            FitnessResult result = Runs.Evaluate(task, ControlNetworks.JoinOnAPair(relay: true));

            List<string> failing = result.Checks!.Select((score, check) => (score, check)).Where(pair => pair.score < 1).Select(pair => task.CheckName(pair.check)).ToList();
            Assert.Contains("a=1,b=5: on time", failing);
            Assert.Contains("a=1,b=5: done once", failing);
        }

        [Fact]
        public void ARelayOfEitherInputMeetsTheMergeContract()
        {
            Assert.Equal("meets the contract", Runs.Evaluate(new ContractTask(FirstParts.Named("merge")), ControlNetworks.JoinOnAPair(relay: true)).Description);
        }

        // Start and x reach both branches; one fires on two spikes and zero on one.
        [Fact]
        public void TwoCoincidenceNeuronsMeetTheSelectContract()
        {
            var network = new Network(new[]
            {
                new Neuron(new[] { Rule.Standard("a", 1) }, 0, new[] { 3, 4 }, false, isInput: true),
                new Neuron(new[] { Rule.Standard("a", 1) }, 0, new[] { 3, 4 }, false, isInput: true),
                new Neuron(new[] { Rule.Standard("aa", 2), Rule.Forget("a", 1) }, 0, new int[0], false),
                new Neuron(new[] { Rule.Standard("a", 1), Rule.Forget("aa", 2) }, 0, new int[0], false),
            });

            Assert.Equal("meets the contract", Runs.Evaluate(new ContractTask(FirstParts.Named("select")), network).Description);
        }

        [Fact]
        public void TwoRelaysFromStartMeetTheForkContract()
        {
            Assert.Equal("meets the contract", Runs.Evaluate(new ContractTask(FirstParts.Named("fork")), ControlNetworks.Fork(apart: false)).Description);
        }

        [Fact]
        public void ForkOutputsOnDifferentStepsFailValues()
        {
            FitnessResult result = Runs.Evaluate(new ContractTask(FirstParts.Named("fork")), ControlNetworks.Fork(apart: true));

            Assert.Equal(0f, result.Checks![(int)ContractRule.Values]);
        }
    }
}
