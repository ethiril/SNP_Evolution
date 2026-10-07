using SnpEvolution.Specs.Parts;

namespace SnpEvolution.Tests.Specs.Parts
{
    public class StopReasonTests
    {
        // Part files keep the reason as text, so the files written before it was typed read back as the same reason.
        [Theory]
        [InlineData("every input checked")]
        [InlineData("bound 6 reached")]
        [InlineData("time limit of 10 s")]
        [InlineData("n=3 has too many computations to follow exactly")]
        [InlineData("counterexample at a=14,b=0,n=0")]
        [InlineData("the contract has no specification")]
        public void AStopReasonReadsBackFromItsText(string text)
        {
            StopReason reason = StopReason.Parse(text);

            Assert.NotEqual(Stop.Other, reason.Stop);
            Assert.Equal(text, reason.ToString());
        }
    }
}
