using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Tasks;

namespace SnpEvolution.Tests.Evolution
{
    public class ArithmeticPartsTests
    {
        public static TheoryData<string> ContractNames => new TheoryData<string>(ArithmeticParts.Contracts.Concat(ArithmeticParts.BuildingBlocks).Select(contract => contract.Name));

        public static TheoryData<int> HandBuilt => new TheoryData<int>(Enumerable.Range(0, HandBuiltParts.All().Count));

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

            Assert.Equal(contract.ToJson(), Contract.FromJson(contract.ToJson()).ToJson());
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
        [MemberData(nameof(HandBuilt))]
        public void EveryHandBuiltPartMeetsItsContract(int index)
        {
            Part part = HandBuiltParts.All()[index];

            PartMeasurement measurement = PartEvolution.Measure(part);

            Assert.True(measurement.MeetsContract, $"{part.Contract.Name}: {measurement.Description}");
        }
    }
}
