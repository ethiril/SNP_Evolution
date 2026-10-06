using System.Diagnostics;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Networks;
using SnpEvolution.Storage;

namespace SnpEvolution.Tests.Evolution
{
    public class BoundedCheckTests
    {
        private static readonly ProofLimits AMinute = new ProofLimits(TimeSpan.FromMinutes(1));

        // The hand-built register, except that a store holding 41 spikes (n = 20 and start) sends two spikes instead of one.
        internal static Part RegisterFailingAtTwenty()
        {
            Part register = HandBuiltParts.All().Single(part => part.Contract.Name == "register");
            const int Store = 4;
            List<Neuron> neurons = register.Network.Neurons.ToList();
            neurons[Store] = new Neuron(
                new[] { Rule.Standard("a(aa){1,19}|a(aa){21,}", 2), Rule.Standard("a{41}", 2, 2), Rule.Standard("a", 1, 2) }, 0, neurons[Store].Connections, false);
            return register with { Network = new Network(neurons) };
        }

        [Fact]
        public void EveryKnownContractAgreesWithItsSpecification()
        {
            Assert.All(ArithmeticParts.Known, contract =>
            {
                Specification? specification = Specifications.For(contract);
                Assert.True(specification != null, contract.Name);
                Assert.Empty(Specifications.Disagreements(contract, specification!));
            });
        }

        [Fact]
        public void AContractThatOnlySharesAKnownNameIsNotProven()
        {
            Part register = ReferenceParts.Register();
            Contract doubled = register.Contract with { Cases = register.Contract.Cases.Select(@case => @case with { Outputs = new Dictionary<string, int> { ["out"] = 2 * @case.Outputs["out"] } }).ToList() };

            BoundedResult result = BoundedCheck.Prove(register with { Contract = doubled }, AMinute);

            Assert.Equal(-1, result.Proven.UpTo);
            Assert.Contains("disagree", result.Proven.Stopped);
        }

        [Fact]
        public void ProvesTheRegisterReferencePartToAtLeast32WithinAMinute()
        {
            var clock = Stopwatch.StartNew();

            BoundedResult result = BoundedCheck.Prove(ReferenceParts.Register(), AMinute with { MaxBound = 32 });

            Assert.Null(result.Counterexample);
            Assert.Equal(32, result.Proven.UpTo);
            Assert.True(clock.Elapsed < TimeSpan.FromMinutes(1), $"took {clock.Elapsed}");
        }

        [Fact]
        public void CatchesAPartThatFailsOnlyAtTwentyWithItsTrace()
        {
            Part broken = RegisterFailingAtTwenty();
            Assert.True(PartEvolution.Measure(broken).MeetsContract);

            BoundedResult result = BoundedCheck.Prove(broken, AMinute);

            Assert.Equal(19, result.Proven.UpTo);
            Assert.Equal("n=20", result.Proven.FailsAt);
            Counterexample counterexample = Assert.IsType<Counterexample>(result.Counterexample);
            Assert.Equal("n=20", counterexample.Inputs);
            Assert.Equal("done,out=20", counterexample.Expected);
            Assert.NotEqual(counterexample.Expected, counterexample.Read);
            Assert.StartsWith("Spikes held after each step", counterexample.Trace);
            Assert.Contains("start", counterexample.Trace.Split('\n')[1]);
        }

        [Fact]
        public void APartIsNotAdmittedWhenItFailsWithinTwiceItsLargestCase()
        {
            var log = new List<string>();

            Assert.Null(BoundedCheck.Admit(RegisterFailingAtTwenty(), log.Add));
            Assert.Contains(log, line => line.Contains("Not admitted") && line.Contains("n=20"));
            Assert.Equal(24, BoundedCheck.Admission(FirstParts.Named("register")).MaxBound);
        }

        [Fact]
        public void APartWithNoDataInPortsIsProvenForEveryInput()
        {
            BoundedResult result = BoundedCheck.Prove(ReferenceParts.Delay(2), AMinute);

            Assert.True(result.Proven.AllInputs);
            Assert.Equal(0, result.Proven.UpTo);
        }

        [Fact]
        public void ACaseTooWideToFollowExactlyIsNotProven()
        {
            BoundedResult result = BoundedCheck.Prove(ReferenceParts.Register(), AMinute with { MaxConfigurations = 0 });

            Assert.Null(result.Counterexample);
            Assert.Equal(-1, result.Proven.UpTo);
            Assert.Contains("too many computations", result.Proven.Stopped);
        }

        [Fact]
        public void EveryHandBuiltPartIsProvenToItsAdmissionBound()
        {
            Assert.All(HandBuiltParts.All(), part =>
            {
                BoundedResult result = BoundedCheck.Prove(part, BoundedCheck.Admission(part.Contract) with { Time = TimeSpan.FromMinutes(1) });

                Assert.True(result.Counterexample == null, $"{part.Contract.Name}: {result.Counterexample}");
                Assert.Equal(BoundedCheck.Admission(part.Contract).MaxBound, result.Proven.UpTo);
            });
        }

        [Fact]
        public void InputsAtABoundHaveThatLargestValueAndIntervalsStartAtOne()
        {
            Assert.Equal(16 - 9, BoundedCheck.InputsWithLargest(FirstParts.Named("add"), 3).Count());
            Assert.Empty(BoundedCheck.InputsWithLargest(FirstParts.Named("interval to count"), 0));
            Assert.Equal(new[] { 1 }, BoundedCheck.InputsWithLargest(FirstParts.Named("interval to count"), 1).Select(inputs => inputs["n"]));
        }

        [Fact]
        public void ALibraryFileKeepsItsProvenBound()
        {
            Part delay = ReferenceParts.Delay(2);
            LibraryPart part = LibraryPart.Of(delay, PartEvolution.Measure(delay), new PartOrigin(0, "by hand", 0)) with { Proven = new ProvenBound(0, true, "every input checked") };

            LibraryPart read = PartLibraryFiles.Read(PartLibraryFiles.ToJson(part), "delay-2.json");

            Assert.Equal(part.Proven, read.Proven);
        }

        [Fact]
        public void ALibraryFileWithACounterexampleIsRefused()
        {
            Part broken = RegisterFailingAtTwenty();
            LibraryPart part = LibraryPart.Of(broken, PartEvolution.Measure(broken), new PartOrigin(0, "by hand", 0)) with { Proven = BoundedCheck.Prove(broken, AMinute).Proven };

            var refusal = Assert.Throws<InvalidDataException>(() => PartLibraryFiles.Read(PartLibraryFiles.ToJson(part), "register.json"));

            Assert.Contains("n=20", refusal.Message);
        }
    }
}
