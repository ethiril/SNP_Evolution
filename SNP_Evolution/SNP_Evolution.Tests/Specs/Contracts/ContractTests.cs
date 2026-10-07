using SnpEvolution.Specs.Contracts;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Specs.Contracts
{
    public class ContractTests
    {
        private static Dictionary<string, int> Values(params (string Port, int Value)[] values) => values.ToDictionary(pair => pair.Port, pair => pair.Value);

        private static readonly Port Start = Port.In("start", PortKind.Trigger);
        private static readonly Port Done = Port.Out("done", PortKind.Trigger);

        private static Contract Increment() => new Contract(
            "increment",
            Start,
            new[] { Done },
            new[] { Port.In("n", PortKind.Count), Port.Out("out", PortKind.Count) },
            Enumerable.Range(0, 4).Select(n => new ContractCase(Values(("n", n)), Values(("out", n + 1)), "done")).ToList(),
            MaxLatency: 8);

        private static Contract ZeroTest() => new Contract(
            "zero test",
            Start,
            new[] { Port.Out("zero", PortKind.Trigger), Port.Out("nonzero", PortKind.Trigger) },
            new[] { Port.In("n", PortKind.Count) },
            Enumerable.Range(0, 3).Select(n => new ContractCase(Values(("n", n)), Values(), n == 0 ? "zero" : "nonzero")).ToList(),
            MaxLatency: 5);

        private static Contract BinaryAdd() => new Contract(
            "add",
            Start,
            new[] { Done },
            new[] { Port.In("a", PortKind.Binary, 4), Port.In("b", PortKind.Binary, 4), Port.Out("sum", PortKind.Binary, 5) },
            new[] { new ContractCase(Values(("a", 3), ("b", 9)), Values(("sum", 12)), "done") },
            MaxLatency: 6);

        public static TheoryData<Contract> WellFormed => new TheoryData<Contract> { PartFixtures.DelayContract(3), Increment(), ZeroTest(), BinaryAdd() };

        [Theory]
        [MemberData(nameof(WellFormed))]
        public void WellFormedContractsHaveNoProblems(Contract contract) => Assert.Empty(contract.Problems());

        [Theory]
        [MemberData(nameof(WellFormed))]
        public void RoundTripsThroughJsonUnchanged(Contract contract)
        {
            string json = Json.Write(contract);
            Contract read = Json.Read<Contract>(json)!;

            Assert.Equal(json, Json.Write(read));
            Assert.Equal(contract.Start, read.Start);
            Assert.Equal(contract.Done, read.Done);
            Assert.Equal(contract.Data, read.Data);
            Assert.Equal(contract.MaxLatency, read.MaxLatency);
            Assert.Equal(contract.MinLatency, read.MinLatency);
            Assert.Equal(contract.Cases.Select(@case => @case.Done), read.Cases.Select(@case => @case.Done));
            Assert.Equal(contract.Cases.SelectMany(@case => @case.Inputs), read.Cases.SelectMany(@case => @case.Inputs));
            Assert.Equal(contract.Cases.SelectMany(@case => @case.Outputs), read.Cases.SelectMany(@case => @case.Outputs));
        }

        [Fact]
        public void JsonNamesKindsAndDirections()
        {
            string json = Json.Write(Increment());

            Assert.Contains("\"Count\"", json);
            Assert.Contains("\"Out\"", json);
        }

        public static TheoryData<Contract, string> Malformed => new TheoryData<Contract, string>
        {
            { Increment() with { Data = new[] { Port.In("n", PortKind.Count), Port.Out("n", PortKind.Count) } }, "Port name 'n' is used 2 times" },
            { Increment() with { Done = new[] { Port.Out("start", PortKind.Trigger) } }, "Port name 'start' is used 2 times" },
            { Increment() with { Start = Port.In("start", PortKind.Count) }, "Start port 'start' must be an in-port of kind Trigger" },
            { Increment() with { OrderedTriggers = true, TogetherTriggers = true }, "cannot both fire in order and fire together" },
            { Increment() with { Done = Array.Empty<Port>() }, "no done port" },
            { Increment() with { Done = new[] { Port.In("done", PortKind.Trigger) } }, "Done port 'done' must be an out-port of kind Trigger" },
            { Increment() with { Cases = Array.Empty<ContractCase>() }, "no test cases" },
            { Increment() with { MaxLatency = 0 }, "maximum latency is 0" },
            { Increment() with { MinLatency = 9 }, "minimum latency is 9" },
            { Increment() with { Cases = new[] { new ContractCase(Values(), Values(("out", 1)), "done") } }, "Case 1 gives no value for data in-port 'n'" },
            { Increment() with { Cases = new[] { new ContractCase(Values(("n", 1)), Values(), "done") } }, "Case 1 gives no value for data out-port 'out'" },
            { Increment() with { Cases = new[] { new ContractCase(Values(("n", 1), ("m", 2)), Values(("out", 2)), "done") } }, "'m', which is not a data in-port" },
            { Increment() with { Cases = new[] { new ContractCase(Values(("n", 1)), Values(("out", 2)), "finished") } }, "'finished', which is not one of the done ports" },
            { Increment() with { Cases = new[] { new ContractCase(Values(("n", -1)), Values(("out", 0)), "done") } }, "-1 for 'n', which is negative" },
            { BinaryAdd() with { Cases = new[] { new ContractCase(Values(("a", 16), ("b", 0)), Values(("sum", 16)), "done") } }, "16 for 'a', which does not fit in 4 bits" },
            { BinaryAdd() with { Data = new[] { Port.In("a", PortKind.Binary) } }, "Binary port 'a' has width 0" },
            { Increment() with { Data = new[] { Port.In("n", PortKind.Count, 3), Port.Out("out", PortKind.Count) } }, "Port 'n' is not binary, so its width must be 0" },
            { Increment() with { Name = "" }, "no name" },
        };

        [Theory]
        [MemberData(nameof(Malformed))]
        public void ValidationNamesWhatIsWrong(Contract contract, string problem)
        {
            Assert.Contains(contract.Problems(), message => message.Contains(problem));
            Assert.Contains(problem, Assert.Throws<ArgumentException>(() => contract.Validated()).Message);
        }

        [Fact]
        public void IntervalInputsMustBeAtLeastOne()
        {
            Contract contract = Increment() with
            {
                Data = new[] { Port.In("n", PortKind.Interval), Port.Out("out", PortKind.Count) },
                Cases = new[] { new ContractCase(Values(("n", 0)), Values(("out", 1)), "done") },
            };

            Assert.Contains(contract.Problems(), message => message.Contains("0 for 'n', which is not a valid interval"));
        }

        [Fact]
        public void CaseLabelsNameEachInput()
        {
            Contract contract = BinaryAdd();

            Assert.Equal("a=3,b=9", contract.Cases[0].Label(contract.DataIn));
        }
    }
}
