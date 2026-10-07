using SnpEvolution.Search.Proposals;

namespace SnpEvolution.Tests.Search.Proposals
{
    public class RecurrenceProposerTests
    {
        private static IReadOnlyList<string> Parts(ShapeProposal? proposal) => proposal!.Contracts.Select(contract => contract.Name).Order().ToList();

        [Fact]
        public void FibonacciGapsAskForTwoRegistersAndAnAdd() =>
            Assert.Equal(new[] { "add", "register", "register" }, Parts(RecurrenceProposer.Propose(new[] { 1, 1, 2, 3, 5, 8, 13, 21 })));

        [Fact]
        public void PowersOfTwoAskForARegisterAndADouble() =>
            Assert.Equal(new[] { "double", "register" }, Parts(RecurrenceProposer.Propose(new[] { 1, 2, 4, 8, 16, 32 })));

        [Fact]
        [Slow]
        public void ARandomSequenceAsksForNothing()
        {
            var random = new Random(31);
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int[] gaps = Enumerable.Range(0, 10).Select(_ => random.Next(1, 40)).ToArray();

                Assert.Null(RecurrenceProposer.Propose(gaps));
            }
        }

        // 2, 4, 6, 8 fits no linear recurrence with coefficients of 0 or more, but its gaps grow by 2 each time.
        [Fact]
        public void GapsWithAConstantDifferenceAskForARegisterAndAnAdd()
        {
            ShapeProposal proposal = RecurrenceProposer.Propose(new[] { 2, 4, 6, 8, 10 })!;

            Assert.Equal(new[] { "add", "register" }, Parts(proposal));
            Assert.Equal("every difference of the gaps is 2", proposal.Form);
            Assert.Equal(new[] { "increment", "register" }, Parts(RecurrenceProposer.Propose(new[] { 3, 4, 5, 6, 7 })));
            Assert.Null(RecurrenceProposer.Propose(new[] { 10, 8, 6, 4, 2 }));
        }
    }
}
