using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Verification;
using Xunit.Abstractions;

namespace SnpEvolution.Tests.Evolution
{
    public class HandBuiltMachinesTests
    {
        private readonly ITestOutputHelper output;

        public HandBuiltMachinesTests(ITestOutputHelper output) => this.output = output;

        [Fact]
        public void TheAddLoopMeetsItsContractAndDrainsALargeAccumulatorBeforeDone()
        {
            var library = new ModuleLibrary();
            foreach (Part part in HandBuiltParts.All())
            {
                library.AddPart(ModuleFixtures.Verified(part), "a test");
            }
            (Composition loop, PortBinding binding) = HandBuiltMachines.AddLoop(library);
            Contract contract = ArithmeticParts.AddLoop();
            Specification specification = Assert.IsType<Specification>(Specification.For(contract));
            ContractCase largeA = specification.Expected(new Dictionary<string, int> { ["a"] = 14, ["b"] = 0, ["n"] = 0 });

            PartMeasurement measurement = Verifier.Measure(new Part(contract with { Cases = contract.Cases.Append(largeA).ToList() }, loop.Flatten(library), binding));

            output.WriteLine($"{measurement.Cost}, latency {measurement.Latency}");
            output.WriteLine(measurement.Description);
            Assert.IsType<Verdict.Passed>(measurement.Verdict);
        }

        [Fact]
        [Slow]
        public void TheHandBuiltLibraryHoldsThePromotedAddLoopBuiltFromParts()
        {
            var log = new List<string>();

            ModuleLibrary library = HandBuiltMachines.Library(log.Add);

            LibraryPart loop = library.PartFor("add loop")!.Part!;
            Assert.True(loop.IsComposite);
            Assert.False(library.PartFor("register")!.Part!.IsComposite);
            Assert.Equal(7, loop.Recipe!.Parts.Count);
            Assert.True(loop.Cost.Neurons > ModuleLibrary.MaxModuleNeurons);
            Assert.Equal(BoundedCheck.Admission(loop.Contract).MaxBound, loop.Proven?.UpTo);
            output.WriteLine(string.Join(Environment.NewLine, log));
        }
    }
}
