using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Verification;

namespace SnpEvolution.Tests.Specs.Verification
{
    public class BoundedInputsTests
    {
        [Fact]
        public void InputsAtABoundHaveThatLargestValueAndIntervalsStartAtOne()
        {
            // Add's pairs with both operands up to 3, less those with both up to 2.
            const int PairsUpToThree = 4 * 4, PairsUpToTwo = 3 * 3;

            Assert.Equal(PairsUpToThree - PairsUpToTwo, BoundedInputs.WithLargest(FirstParts.Named("add"), 3).Count());
            Assert.Empty(BoundedInputs.WithLargest(FirstParts.Named("interval to count"), 0));
            Assert.Equal(new[] { 1 }, BoundedInputs.WithLargest(FirstParts.Named("interval to count"), 1).Select(inputs => inputs["n"]));
        }
    }
}
