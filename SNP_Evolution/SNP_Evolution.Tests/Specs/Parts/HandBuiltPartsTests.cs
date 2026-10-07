using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Verification;

namespace SnpEvolution.Tests.Specs.Parts
{
    public class HandBuiltPartsTests
    {
        [Theory]
        [MemberData(nameof(PartFixtures.HandBuilt), MemberType = typeof(PartFixtures))]
        public void EveryHandBuiltPartMeetsItsContract(int index)
        {
            Part part = HandBuiltParts.All()[index];

            PartMeasurement measurement = Verifier.Measure(part, new EvaluationBudget());

            Assert.True(measurement.Verdict is Verdict.Passed, $"{part.Contract.Name}: {measurement.Description}");
        }
    }
}
