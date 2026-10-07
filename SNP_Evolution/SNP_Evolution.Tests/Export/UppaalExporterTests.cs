using System.Xml;
using SnpEvolution.Export;
using SnpEvolution.Model;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Tests.Export
{
    public class UppaalExporterTests
    {
        private const string TraceQueryName = "every neuron holds what our engine's run holds, on every step";
        private const string DoneOnceQueryName = "done once, the right one, with the right outputs";

        private static IReadOnlyList<bool> Check(Part part)
        {
            using var temp = new TempFolder("snp-uppaal");
            return Verifyta.Check(UppaalExporter.Export(part), temp.Path);
        }

        private static IReadOnlyList<ContractCase> CasesUpTo(Contract contract, int largest) =>
            Enumerable.Range(0, largest + 1).Select(n => Specification.For(contract)!.Expected(new Dictionary<string, int> { ["n"] = n })).ToList();

        private static bool Holds(Part part, string query)
        {
            UppaalModel model = UppaalExporter.Export(part);
            return Check(part)[model.QueryNames.ToList().IndexOf(query)];
        }

        [Fact]
        public void ExportsWellFormedXmlWithAQueryPerContractRule()
        {
            UppaalModel model = UppaalExporter.Export(RepositoryFiles.Part("parts", "delay-2.json"));

            using var reader = XmlReader.Create(new StringReader(model.Model), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
            while (reader.Read())
            {
            }
            Assert.Equal("delay-2", model.Name);
            Assert.Equal(UppaalExporter.ContractQueries.Select(query => query.Name).Append(TraceQueryName), model.QueryNames);
            Assert.Contains(UppaalExporter.TraceQuery, model.Queries);
            Assert.Contains("system Clock, N1, N2;", model.Model);
        }

        [Fact]
        public void TheRegisterModelStartsAndReadsEachCaseAsTheEngineDoes()
        {
            Part register = PartFixtures.HandBuiltRegister();
            var task = new ContractTask(register.Contract, register.Binding);

            string model = UppaalExporter.Export(register).Model;

            string starts = string.Join(", ", task.Cases.Select(@case => @case.Input.StepsPerInput[0][0]));
            string outs = string.Join(", ", register.Contract.Cases.Select(@case => $"{{{@case.Outputs["out"]}}}"));
            Assert.Contains($"const int START[CASES] = {{{starts}}};", model);
            Assert.Contains($"const int EXPECTED_OUT[CASES][OUTS] = {{{outs}}};", model);
            Assert.Contains($"const int AFTER_DONE = {register.Contract.MaxLatency};", model);
        }

        [Fact]
        public void VerifytaVerdictsAreReadInQueryOrder()
        {
            string output = "Verifying formula 1 at /tmp/delay-2.q:2\n -- Formula is satisfied.\nVerifying formula 2 at /tmp/delay-2.q:4\n -- Formula is NOT satisfied.\n";

            Assert.Equal(new[] { true, false }, Verifyta.Verdicts(output));
        }

        [Fact]
        public void ANondeterministicPartHasNoTraceQuery()
        {
            Part delay = ReferenceParts.Delay(2);
            Neuron start = delay.Network.Neurons[0];
            var choosing = delay with { Network = new Network(new[] { start.WithRules(start.Rules.Append(Rule.Standard("a", 1, 1))), delay.Network.Neurons[1] }) };

            Assert.DoesNotContain(UppaalExporter.TraceQuery, UppaalExporter.Export(choosing).Queries);
        }

        [Fact]
        public void RefusesALegacyRuleWithADelay()
        {
            Part delay = ReferenceParts.Delay(2);
            var legacy = delay with { Network = new Network(new[] { delay.Network.Neurons[0], delay.Network.Neurons[1].WithRules(new[] { new Rule("a+", 1, true) }) }) };

            var refusal = Assert.Throws<ArgumentException>(() => UppaalExporter.Export(legacy));

            Assert.Contains("legacy rule with a delay", refusal.Message);
        }

        [VerifytaFact]
        public void TheEvolvedDelayMeetsEveryContractRuleInUppaal()
        {
            Assert.All(Check(RepositoryFiles.Part("parts", "delay-2.json")), Assert.True);
        }

        [VerifytaFact]
        public void TheEvolvedSequencerAndProfileDelayMeetEveryContractRuleInUppaal()
        {
            Assert.All(Check(RepositoryFiles.Part("parts", "sequencer-2.json")), Assert.True);
            Assert.All(Check(RepositoryFiles.Part("parts-profile", "delay-2.json")), Assert.True);
        }

        [VerifytaFact]
        public void UppaalFindsADelayThatFiresDoneTwice()
        {
            Part twice = PartFixtures.DelayFiringDoneTwice(2);

            Assert.False(Holds(twice, DoneOnceQueryName));
            Assert.True(Holds(twice, TraceQueryName));
        }

        [VerifytaFact]
        public void TheHandBuiltRegisterMeetsEveryContractRuleOnEveryCaseInUppaal()
        {
            Assert.All(Check(PartFixtures.HandBuiltRegister()), Assert.True);
        }

        [VerifytaFact]
        public void UppaalAgreesWithTheBoundedCheckOnARegisterFailingAtTwenty()
        {
            Part broken = PartFixtures.RegisterFailingAtTwenty();
            Contract upToTwenty = broken.Contract with { Cases = CasesUpTo(broken.Contract, 20), MaxLatency = Specifications.LatencyFor(20) };

            Assert.False(Holds(broken with { Contract = upToTwenty }, DoneOnceQueryName));
        }
    }
}
