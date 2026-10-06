using System.Xml;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Export;
using SnpEvolution.Networks;
using SnpEvolution.Storage;
using SnpEvolution.Tests.Evolution;

namespace SnpEvolution.Tests.Export
{
    public class UppaalExporterTests
    {
        private static Part LibraryPart(string folder, string file) => RepositoryFiles.ReadPart(folder, file).Part;

        private static string Folder() => Path.Combine(Path.GetTempPath(), "snp-uppaal-" + Guid.NewGuid().ToString("N"));

        private static IReadOnlyList<bool> Check(Part part)
        {
            string folder = Folder();
            try
            {
                return Verifyta.Check(UppaalExporter.Export(part), folder);
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        private static bool Holds(Part part, string query)
        {
            UppaalModel model = UppaalExporter.Export(part);
            return Check(part)[model.QueryNames.ToList().IndexOf(query)];
        }

        [Fact]
        public void ExportsWellFormedXmlWithAQueryPerContractRule()
        {
            UppaalModel model = UppaalExporter.Export(LibraryPart("parts", "delay-2.json"));

            using var reader = XmlReader.Create(new StringReader(model.Model), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
            while (reader.Read())
            {
            }
            Assert.Equal("delay-2", model.Name);
            Assert.Equal(UppaalExporter.ContractQueries.Select(query => query.Name).Append("every neuron holds what our engine's run holds, on every step"), model.QueryNames);
            Assert.Contains(UppaalExporter.TraceQuery, model.Queries);
            Assert.Contains("system Clock, N1, N2;", model.Model);
        }

        [Fact]
        public void TheRegisterModelStartsAndReadsEachCaseAsTheEngineDoes()
        {
            Part register = HandBuiltParts.All().Single(part => part.Contract.Name == "register");
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
            Assert.All(Check(LibraryPart("parts", "delay-2.json")), Assert.True);
        }

        [VerifytaFact]
        public void TheEvolvedSequencerAndProfileDelayMeetEveryContractRuleInUppaal()
        {
            Assert.All(Check(LibraryPart("parts", "sequencer-2.json")), Assert.True);
            Assert.All(Check(LibraryPart("parts-profile", "delay-2.json")), Assert.True);
        }

        [VerifytaFact]
        public void UppaalFindsADelayThatFiresDoneTwice()
        {
            Part twice = ReferenceParts.DelayFiringDoneTwice(2);

            Assert.False(Holds(twice, "done once, the right one, with the right outputs"));
            Assert.True(Holds(twice, "every neuron holds what our engine's run holds, on every step"));
        }

        [VerifytaFact]
        public void TheHandBuiltRegisterMeetsEveryContractRuleOnEveryCaseInUppaal()
        {
            Assert.All(Check(HandBuiltParts.All().Single(part => part.Contract.Name == "register")), Assert.True);
        }

        [VerifytaFact]
        public void UppaalAgreesWithTheBoundedCheckOnARegisterFailingAtTwenty()
        {
            Part broken = BoundedCheckTests.RegisterFailingAtTwenty();
            Contract upToTwenty = broken.Contract with { Cases = Enumerable.Range(0, 21).Select(n => Specifications.For(broken.Contract)!.Expected(new Dictionary<string, int> { ["n"] = n })).ToList(), MaxLatency = FirstParts.LatencyFor(20) };

            Assert.False(Holds(broken with { Contract = upToTwenty }, "done once, the right one, with the right outputs"));
        }
    }
}
