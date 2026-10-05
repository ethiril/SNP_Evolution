using SnpEvolution.Evolution.Contracts;

namespace SnpEvolution.Tests.Simulation
{
    public class PortEncodingTests
    {
        [Fact]
        public void StartAndTriggerAreOneSpike()
        {
            Assert.Equal(new[] { 0 }, PortEncoding.Start());
            Assert.Equal(new[] { 4 }, PortEncoding.Start(4));
            Assert.Equal(new[] { 3 }, PortEncoding.Trigger(3));
        }

        [Theory]
        [InlineData(1, new[] { 2, 3 })]
        [InlineData(5, new[] { 2, 7 })]
        public void IntervalIsTwoSpikesNApart(int n, int[] steps) => Assert.Equal(steps, PortEncoding.Interval(n, from: 2));

        [Fact]
        public void AnIntervalOfZeroIsRejected()
        {
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => PortEncoding.Interval(0));

            Assert.Contains("n must be at least 1", error.Message);
        }

        [Theory]
        [InlineData(0, new int[0])]
        [InlineData(1, new[] { 2 })]
        [InlineData(5, new[] { 2, 3, 4, 5, 6 })]
        public void CountIsNSpikesOnePerStep(int n, int[] steps) => Assert.Equal(steps, PortEncoding.Count(n, from: 2));

        // 5 is 0101 from the least significant bit: spikes on bits 0 and 2.
        [Theory]
        [InlineData(0, new int[0])]
        [InlineData(1, new[] { 2 })]
        [InlineData(5, new[] { 2, 4 })]
        [InlineData(15, new[] { 2, 3, 4, 5 })]
        public void BinarySpikesOnEachOneBitLeastSignificantFirst(int n, int[] steps) => Assert.Equal(steps, PortEncoding.Binary(n, width: 4, from: 2));

        [Fact]
        public void BinaryRejectsAValueWiderThanThePort() => Assert.Throws<ArgumentOutOfRangeException>(() => PortEncoding.Binary(16, width: 4));

        [Fact]
        public void ACaseLoadsUnaryDataBeforeStartAndStreamsBinaryAfter()
        {
            var contract = new Contract(
                "mixed",
                Port.In("start", PortKind.Trigger),
                new[] { Port.Out("done", PortKind.Trigger) },
                new[] { Port.In("c", PortKind.Count), Port.In("b", PortKind.Binary, 3), Port.In("i", PortKind.Interval) },
                new[] { new ContractCase(new Dictionary<string, int> { ["c"] = 2, ["b"] = 6, ["i"] = 4 }, new Dictionary<string, int>(), "done") },
                MaxLatency: 5);

            EncodedCase encoded = PortEncoding.ForCase(contract, contract.Cases[0], quietSteps: 2);

            Assert.Equal(5, encoded.StartStep);
            Assert.Equal(new[] { 5 }, encoded.Input.StepsPerInput[0]);
            Assert.Equal(new[] { 0, 1 }, encoded.Input.StepsPerInput[1]);
            Assert.Equal(new[] { 6, 7 }, encoded.Input.StepsPerInput[2]);
            Assert.Equal(new[] { 0, 4 }, encoded.Input.StepsPerInput[3]);
        }

        [Fact]
        public void StartWaitsForTheQuietSteps()
        {
            Contract delay = ReferenceParts.DelayContract(2);

            EncodedCase encoded = PortEncoding.ForCase(delay, delay.Cases[0], quietSteps: 2);

            Assert.Equal(2, encoded.StartStep);
            Assert.Single(encoded.Input.StepsPerInput);
        }
    }
}
