using SnpEvolution.Cli;
using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Export;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Storage;
using static SnpEvolution.Tests.TestNetworks;

namespace SnpEvolution.Tests.Export
{
    public class VerilogExporterTests
    {
        private static LibraryPart LibraryDelay() =>
            RepositoryFiles.ReadPart("parts", "delay-2.json");

        // Runs every case of the part under iverilog and returns what it printed and what our engine says it should.
        private static (string Simulated, string Expected) CoSimulate(Part part)
        {
            VerilogDesign design = VerilogExporter.Export(part);
            var cases = SpikeTrace.Cases(part.Contract);
            List<InputSpikes> inputs = cases.Select(@case => @case.Input).ToList();
            List<int> steps = cases.Select(@case => @case.Steps).ToList();
            string folder = Path.Combine(Path.GetTempPath(), "snp-verilog-" + Guid.NewGuid().ToString("N"));
            try
            {
                return (Iverilog.Simulate(design, VerilogTestbench.For(design, inputs, steps), folder), VerilogTestbench.ExpectedOutput(part.Network, inputs, steps));
            }
            finally
            {
                Directory.Delete(folder, recursive: true);
            }
        }

        public static TheoryData<string> Parts() => new TheoryData<string> { "library delay 2", "register", "add", "increment" };

        private static Part Named(string name) => name switch
        {
            "library delay 2" => LibraryDelay().Part,
            "register" => ReferenceParts.Register(),
            "add" => ReferenceParts.Add(),
            _ => ReferenceParts.Increment(),
        };

        [Theory]
        [MemberData(nameof(Parts))]
        public void PartsMeetTheirContractsSoTheirTracesAreWorthMatching(string name)
        {
            Assert.True(PartEvolution.Measure(Named(name)).MeetsContract);
        }

        [IverilogFact]
        public void PartsMatchOurEngineStepForStepOnEveryContractCase()
        {
            foreach (string name in Parts())
            {
                (string simulated, string expected) = CoSimulate(Named(name));

                Assert.True(expected == simulated, $"{name} differs from our engine first at: {FirstDifference(expected, simulated)}");
                Assert.EndsWith("overflow 0\n", simulated);
            }
        }

        // Random deterministic networks with every kind of delay: legacy rules that hold the neuron, standard rules that
        // close it and axonal ones, with initial spikes and input from two inputs.
        [IverilogFact]
        public void RandomDeterministicNetworksMatchOurEngineStepForStep()
        {
            var random = new Random(5);
            var space = new GenomeSpace(InputCount: 2, RuleForm: RuleForm.Mixed, MaxNeurons: 6, MaxDelay: 3, MaxRulesPerNeuron: 2);
            var factory = new NetworkFactory(space, new ExpressionGenerator(ExpressionGenerator.ExperimentalTemplates, 4, random), random);
            var input = new InputSpikes(new IReadOnlyList<int>[] { new[] { 0, 1, 1, 4, 9 }, new[] { 2, 3, 7 } });
            int exported = 0;
            var delays = new HashSet<DelayKind>();
            for (int attempt = 0; exported < 25 && attempt < 2000; attempt++)
            {
                Network network = random.Next(3) == 0 ? AxonalCopy(factory.NewNetwork(), random) : factory.NewNetwork();
                if (SpikeTrace.Choices(network).Count > 0)
                {
                    continue;
                }
                exported++;
                delays.UnionWith(network.Neurons.SelectMany(neuron => neuron.Rules).Where(rule => rule.Delay > 0).Select(rule => rule.DelayKind));
                long mostHeld = SpikeTrace.Run(network, input, 40).MostHeld;
                VerilogDesign design = VerilogExporter.Export(network, $"random {attempt}", NetworkPort.Plain(network), mostHeld);
                string folder = Path.Combine(Path.GetTempPath(), "snp-verilog-" + Guid.NewGuid().ToString("N"));
                try
                {
                    string simulated = Iverilog.Simulate(design, VerilogTestbench.For(design, new[] { input }, new[] { 40 }), folder);
                    string expected = VerilogTestbench.ExpectedOutput(network, new[] { input }, new[] { 40 });
                    Assert.True(expected == simulated, $"{NetworkNotation.Format(network)}\ndiffers first at: {FirstDifference(expected, simulated)}");
                }
                finally
                {
                    Directory.Delete(folder, recursive: true);
                }
            }
            Assert.Equal(25, exported);
            Assert.Equal(new[] { DelayKind.Closing, DelayKind.Holding, DelayKind.Axonal }, delays.OrderBy(kind => kind));
        }

        [Fact]
        public void RefusesANondeterministicNetworkAndSaysWhichRulesCompete()
        {
            var network = new Network(new[] { OutputNeuron(3, Standard("a+", 1), Standard("aa(a)*", 2)) });

            var refusal = Assert.Throws<ArgumentException>(() => VerilogExporter.Export(network, "choice", NetworkPort.Plain(network), 3));

            Assert.Contains("Neuron 1 could apply rule 1 (a+/a -> a) or rule 2 (aa(a)*/aa -> a) when it holds 2 spike(s)", refusal.Message);
        }

        [Fact]
        public void FindsRulesThatOnlyCompeteOnceTheNeuronHoldsWhatTheyConsume()
        {
            var network = new Network(new[] { OutputNeuron(0, Standard("a+", 5), Standard("a+", 5, produce: 2)) });

            Assert.Contains("when it holds 5 spike(s)", Assert.Single(SpikeTrace.Choices(network)));
        }

        [Fact]
        public void SizesCountersFromTheRegisterWidth()
        {
            Part register = ReferenceParts.Register();

            VerilogDesign design = VerilogExporter.Export(register);

            long width = PartEvolution.Measure(register).Cost.RegisterWidth;
            Assert.Equal(VerilogExporter.BitsFor(width), design.CounterWidth);
            Assert.Contains("module register (", design.Module);
            Assert.Contains("input  wire [", design.Module);
            Assert.Contains("output wire [", design.Module);
            Assert.Equal(new[] { "start", "n", "out", "done" }, design.Ports.Select(port => port.Name));
        }

        [Fact]
        public void ExportsTheSameTextEveryTime()
        {
            Assert.Equal(VerilogExporter.Export(ReferenceParts.Add()).Module, VerilogExporter.Export(ReferenceParts.Add()).Module);
        }

        private static Network AxonalCopy(Network network, Random random) =>
            new Network(network.Neurons.Select(neuron => neuron.WithRules(neuron.Rules.Select(rule => rule.Delay > 0 && random.Next(2) == 0 ? rule.WithAxonal(true) : rule))).ToList());

        private static string FirstDifference(string expected, string simulated)
        {
            string[] want = expected.Split('\n');
            string[] got = simulated.Split('\n');
            int line = Enumerable.Range(0, Math.Min(want.Length, got.Length)).FirstOrDefault(index => want[index] != got[index], Math.Min(want.Length, got.Length));
            return $"line {line + 1}: expected '{want.ElementAtOrDefault(line)}', got '{got.ElementAtOrDefault(line)}'";
        }
    }
}
