using SnpEvolution.Export;
using SnpEvolution.Model;
using SnpEvolution.Search.Genome;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Verification;
using static SnpEvolution.Tests.Fixtures.TestNetworks;

namespace SnpEvolution.Tests.Export
{
    public class NirExporterTests
    {
        // Parts evolve-parts --profile hardware found, kept in the repository next to parts/.
        private const string ProfileFolder = "parts-profile";

        private static readonly string[] ProfilePartFiles = { "delay-2.json", "sequencer-2.json" };

        public static TheoryData<string> ProfileFiles => new TheoryData<string>(ProfilePartFiles);

        private static string Check(NirDescription description)
        {
            using var temp = new TempFolder("snp-nir");
            return NirExporter.WriteAndCheck(description, temp.Path);
        }

        [Theory]
        [MemberData(nameof(ProfileFiles))]
        public void TheProfilePartsMeetTheirContractsAndFitTheProfile(string file)
        {
            Part part = RepositoryFiles.Part(ProfileFolder, file);

            Assert.True(Verifier.Measure(part, new EvaluationBudget()).Verdict is Verdict.Passed, file);
            Assert.Empty(HardwareProfile.Problems(part.Network));
        }

        // Stays a loop over the files, since the attribute that skips it without the NIR tools is a fact.
        [NirFact]
        public void ProfilePartsLoadInNirAndMatchOurTracesInNorse()
        {
            foreach (string file in ProfilePartFiles)
            {
                string report = Check(NirExporter.Export(RepositoryFiles.Part(ProfileFolder, file)));

                Assert.Contains("matches SN P on all 1 case(s)", report);
            }
        }

        // Random profile networks, with forgetting neurons, thresholds above one, axonal delays, fan-in and two inputs.
        [NirFact]
        public void RandomProfileNetworksMatchOurTracesInNorse()
        {
            var random = new Random(3);
            var space = new GenomeSpace(InputCount: 2, MaxNeurons: 7, MaxDelay: 3, HardwareProfile: true);
            var factory = Factories.Networks(space, random);
            var input = new InputSpikes(new IReadOnlyList<int>[] { new[] { 0, 1, 1, 2, 5, 6, 6, 6 }, new[] { 0, 3, 4, 4, 9 } });
            int firings = 0, delays = 0, forgetting = 0;
            for (int network = 0; network < 4; network++)
            {
                Network made = factory.NewNetwork();
                NirDescription description = NirExporter.Export(made, $"random {network}", NetworkPort.Plain(made), new[] { ("random input", input, 30) });
                firings += description.Cases[0].Applied.Sum(step => step.Count);
                delays += description.Delays.Count(delay => delay > 0);
                forgetting += description.Fires.Count(fires => !fires);

                string report = Check(description);

                Assert.Contains("matches SN P on all 1 case(s)", report);
            }
            Assert.True(firings > 20 && delays > 0 && forgetting > 0, $"{firings} firings, {delays} delays, {forgetting} forgetting neurons");
        }

        [Fact]
        public void RefusesANetworkOutsideTheProfileAndNamesTheRule()
        {
            var network = new Network(new[] { OutputNeuron(0, new Rule("a(aa)*", 0, true)) });

            var refusal = Assert.Throws<ArgumentException>(() => NirExporter.Export(network, "parity", Array.Empty<NetworkPort>(), Array.Empty<(string, InputSpikes, int)>()));

            Assert.Contains("Neuron 1, rule 1 (a(aa)* -> a)", refusal.Message);
        }

        [Fact]
        public void DescribesThresholdsDelaysAndTheTraceToMatch()
        {
            var network = new Network(new[]
            {
                InputNeuron(new[] { 2, 3 }, HardwareProfile.ThresholdRule(1, delay: 2)),
                Neuron(0, new[] { 3 }, HardwareProfile.ThresholdRule(2, fire: false)),
                OutputNeuron(0, HardwareProfile.ThresholdRule(1)),
            });
            var input = new InputSpikes(new IReadOnlyList<int>[] { new[] { 0 } });

            NirDescription description = NirExporter.Export(network, "small", new[] { new NetworkPort("start", 1, true), new NetworkPort("out", 3, false) }, new[] { ("one spike", input, 5) });

            Assert.Equal(new int?[] { 1, 2, 1 }, description.Thresholds);
            Assert.Equal(new[] { true, false, true }, description.Fires);
            Assert.Equal(new[] { 2, 0, 0 }, description.Delays);
            Assert.Equal(new[] { "1,2", "1,3", "2,3" }, description.Synapses.Select(pair => string.Join(",", pair)));
            NirCase run = Assert.Single(description.Cases);
            // n1 fires on step 1; its spike leaves on step 3 and n3 fires on step 4. n2 only ever holds one spike.
            Assert.Equal(new[] { "", "1", "", "", "3" }, run.Applied.Select(step => string.Join(",", step)));
            Assert.Equal(new long[] { 0, 0, 0 }, run.HeldAfterRule[1]);
            Assert.Equal(new long[] { 0, 1, 0 }, run.HeldAfterRule[4]);
        }
    }
}
