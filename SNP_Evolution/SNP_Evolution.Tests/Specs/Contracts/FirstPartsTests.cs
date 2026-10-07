using SnpEvolution.Model;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Specs.Contracts
{
    public class FirstPartsTests
    {
        private const string IssueTable =
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
            "| Sequencer | done out xk | fires its outputs in order, each one step after the previous (k = 2, 3) |\n";

        public static TheoryData<string> ContractNames => new TheoryData<string>(FirstParts.Contracts.Select(contract => contract.Name));

        [Fact]
        public void TenPartsMakeFourteenContracts()
        {
            Assert.Equal(10, FirstParts.All.Count);
            Assert.Equal(14, FirstParts.Contracts.Count);
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
            string table = FirstParts.Table();
            Console.WriteLine(table);

            Assert.Equal(IssueTable, table);
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

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void TheHandBuiltDelayMeetsItsFirstPartContract(int k)
        {
            Part delay = ReferenceParts.Delay(k);

            Assert.Equal(1f, Runs.Evaluate(new ContractTask(FirstParts.Named($"delay {k}"), delay.Binding), delay.Network).Fitness);
        }

        [Fact]
        public void TheHandBuiltRegisterMeetsItsFirstPartContract()
        {
            Part register = ReferenceParts.Register();
            FitnessResult result = Runs.Evaluate(new ContractTask(FirstParts.Named("register"), register.Binding), register.Network);

            Assert.True(result.Exact);
            Assert.Equal("meets the contract", result.Description);
        }

        [Fact]
        public void TheHandBuiltAddMeetsItsFirstPartContract()
        {
            Part add = ReferenceParts.Add();
            FitnessResult result = Runs.Evaluate(add.Task(), add.Network);

            Assert.True(result.Exact);
            Assert.Equal("meets the contract", result.Description);
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
        public void OutputsFiringOutOfOrderFailOnlyDoneOnce()
        {
            FitnessResult result = Runs.Evaluate(new ContractTask(FirstParts.Named("sequencer 2")), SequencerChain(swapped: true));

            Assert.Equal(new[] { 1f, 0f, 1f, 1f }, result.Checks);
        }
    }
}
