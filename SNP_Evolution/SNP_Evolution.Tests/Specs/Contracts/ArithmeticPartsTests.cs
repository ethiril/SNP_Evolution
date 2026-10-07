using SnpEvolution.Search.Benchmarking;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Verification;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Specs.Contracts
{
    public class ArithmeticPartsTests
    {
        public static TheoryData<string> ContractNames => new TheoryData<string>(ArithmeticParts.Contracts.Concat(ArithmeticParts.BuildingBlocks).Select(contract => contract.Name));

        [Fact]
        public void FourOperationsInTwoEncodingsMakeEightContracts()
        {
            Assert.Equal(8, ArithmeticParts.Contracts.Count);
            Assert.Equal(
                new[] { "subtract", "multiply", "divide", "compare", "subtract 4-bit", "multiply 4-bit", "divide 4-bit", "compare 4-bit" },
                ArithmeticParts.Contracts.Select(contract => contract.Name));
            Assert.Equal(ArithmeticParts.Known.Count, ArithmeticParts.Known.Select(contract => contract.Name).Distinct().Count());
        }

        [Theory]
        [MemberData(nameof(ContractNames))]
        public void EveryContractValidates(string name) => Assert.Empty(ArithmeticParts.Named(name).Problems());

        [Theory]
        [MemberData(nameof(ContractNames))]
        public void EveryContractRoundTripsThroughJson(string name)
        {
            Contract contract = ArithmeticParts.Named(name);

            Assert.Equal(Json.Write(contract), Json.Write(Json.Read<Contract>(Json.Write(contract))!));
        }

        [Fact]
        public void CasesIncludeZeroOperandsAndALargerCase()
        {
            foreach (Contract contract in ArithmeticParts.Count)
            {
                Assert.Contains(contract.Cases, @case => @case.Inputs["a"] == 0);
                Assert.Contains(contract.Cases, @case => @case.Inputs["a"] > ArithmeticParts.LargestFactor);
            }
            Assert.Contains(ArithmeticParts.Named("multiply").Cases, @case => @case.Inputs["b"] == 0);
            Assert.Contains(ArithmeticParts.Named("multiply 4-bit").Cases, @case => @case.Outputs["product"] == 225);
        }

        [Fact]
        public void CasesHoldTheRightAnswers()
        {
            ContractCase Case(string contract, int a, int b) => ArithmeticParts.Named(contract).Cases.Single(@case => @case.Inputs["a"] == a && @case.Inputs["b"] == b);

            Assert.Equal("not less", Case("compare", 3, 3).Done);
            Assert.Equal("less", Case("compare", 2, 3).Done);
            Assert.Equal(new[] { 3, 1 }, new[] { Case("divide", 7, 2).Outputs["quotient"], Case("divide", 7, 2).Outputs["remainder"] });
            Assert.Equal(12, Case("multiply", 4, 3).Outputs["product"]);
        }

        [Fact]
        public void BinaryContractsUseFourBitOperandsAndAnEightBitProduct()
        {
            Assert.All(ArithmeticParts.Binary.SelectMany(contract => contract.DataIn), port => Assert.Equal(4, port.Width));
            Assert.Equal(8, ArithmeticParts.Named("multiply 4-bit").DataOut.Single().Width);
        }

        [Fact]
        public void EveryArithmeticContractIsATaskInTheSuite()
        {
            List<string> names = TaskSuite.All.Select(task => task.Name).ToList();

            Assert.All(ArithmeticParts.Contracts, contract => Assert.Contains("Contract " + contract.Name, names));
        }

        [Theory]
        [MemberData(nameof(PartFixtures.HandBuilt), MemberType = typeof(PartFixtures))]
        public void EveryHandBuiltPartMeetsItsContract(int index)
        {
            Part part = HandBuiltParts.All()[index];

            PartMeasurement measurement = Verifier.Measure(part, new EvaluationBudget());

            Assert.True(measurement.Verdict is Verdict.Passed, $"{part.Contract.Name}: {measurement.Description}");
        }

        [Fact]
        public void EveryKnownContractFindsTheSpecificationItIsBuiltFrom()
        {
            Assert.All(ArithmeticParts.KnownEntries, entry => Assert.Same(entry.Specification, Specification.For(entry.Contract)));
        }
    }
}
