using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Simulation.Metal;
using Xunit.Sdk;
using static SnpEvolution.Tests.TestNetworks;

namespace SnpEvolution.Tests.Simulation
{
    // Skipped on machines without a Metal GPU, and slow since it runs on the GPU.
    [TraitDiscoverer(SpeedDiscoverer.TypeName, SpeedDiscoverer.AssemblyName)]
    public sealed class MetalFactAttribute : FactAttribute, ITraitAttribute
    {
        public MetalFactAttribute()
        {
            if (!MetalEngine.IsAvailable)
            {
                Skip = "This machine has no Metal GPU.";
            }
        }
    }

    public class MetalEngineTests
    {
        private static readonly string[] Expressions = { "a", "aa", "a+", "a(aa)*", "aaa", "a(aa)+", "(aa)+" };

        // A network where at most one rule can ever apply in a neuron has a single computation, so the GPU must match
        // the CPU exactly however the two choose rules.
        private static Network SingleRuleNetwork(int neuronCount, int inputCount, Random random)
        {
            var neurons = new List<Neuron>();
            for (int index = 0; index < neuronCount; index++)
            {
                string expression = Expressions[random.Next(Expressions.Length)];
                int delay = random.Next(3) == 0 ? random.Next(1, 3) : 0;
                Rule rule = random.Next(2) == 0
                    ? new Rule(expression, delay, random.Next(5) != 0)
                    : new Rule(expression, delay, random.Next(5) != 0, consume: 1, produce: random.Next(1, 3));
                var connections = new SortedSet<int>();
                for (int synapse = random.Next(1, 5); synapse > 0; synapse--)
                {
                    int target = random.Next(neuronCount);
                    if (target != index)
                    {
                        connections.Add(target + 1);
                    }
                }
                neurons.Add(new Neuron(new[] { rule }, index < inputCount ? 0 : random.Next(3), connections.ToList(), index == neuronCount - 1, index < inputCount));
            }
            return new Network(neurons);
        }

        private static void AssertSameResults(IReadOnlyList<Trial> trials, SimulationOptions options)
        {
            IReadOnlyList<TrialResult> cpu = new SequentialCpuEngine().Run(trials, options, new Random(1));
            IReadOnlyList<TrialResult> gpu = MetalEngine.OrCpu(gpuThreshold: 0).Run(trials, options, new Random(1));

            for (int index = 0; index < trials.Count; index++)
            {
                Assert.Equal(cpu[index].Outputs, gpu[index].Outputs);
                Assert.Equal(cpu[index].CanHalt, gpu[index].CanHalt);
                Assert.Equal(cpu[index].SpikeTrains, gpu[index].SpikeTrains);
            }
        }

        [MetalFact]
        public void MatchesTheCpuOnSmallSingleComputationNetworks()
        {
            var delayed = new Network(new[]
            {
                Neuron(2, new[] { 2, 3 }, Standard("aa", 2, produce: 2, delay: 2)),
                Neuron(0, new[] { 3 }, new Rule("a(aa)*", 1, true)),
                OutputNeuron(0, Standard("a+", 1)),
            });
            var trials = new List<Trial>();
            foreach (Readout readout in Enum.GetValues<Readout>())
            {
                trials.Add(new Trial(Identity(), InputSpikes.Numbers(3), readout));
                trials.Add(new Trial(Identity(), InputSpikes.Numbers(7), readout));
                trials.Add(new Trial(AlwaysOutputsOne(), InputSpikes.None, readout));
                trials.Add(new Trial(PingPong(), InputSpikes.None, readout));
                trials.Add(new Trial(NeverOutputs(), InputSpikes.None, readout));
                trials.Add(new Trial(delayed, InputSpikes.None, readout));
            }

            AssertSameResults(trials, new SimulationOptions(MaxSteps: 40, Repetitions: 10, OutputTiming.Legacy));
            AssertSameResults(trials, new SimulationOptions(MaxSteps: 40, Repetitions: 10, OutputTiming.Interval));
            AssertSameResults(trials, new SimulationOptions(MaxSteps: 3, Repetitions: 10));
        }

        [MetalFact]
        public void MatchesTheCpuOnLargeSingleComputationNetworks()
        {
            // Thousands of neurons spread several to a thread, with inputs, delays and both rule forms.
            var random = new Random(4);
            var trials = new List<Trial>();
            foreach (Readout readout in Enum.GetValues<Readout>())
            {
                trials.Add(new Trial(SingleRuleNetwork(3000, 0, random), InputSpikes.None, readout));
                trials.Add(new Trial(SingleRuleNetwork(5000, 2, random), InputSpikes.Numbers(4, 9), readout));
                trials.Add(new Trial(SingleRuleNetwork(40, 1, random), InputSpikes.Numbers(5), readout));
            }

            AssertSameResults(trials, new SimulationOptions(MaxSteps: 120, Repetitions: 3));
        }

        [MetalFact]
        public void SamplesOnlyOutputsTheNetworkCanProduce()
        {
            var options = new SimulationOptions(MaxSteps: 30, Repetitions: 400);
            Network[] networks = { ReferenceNetworks.NaturalNumbers(), ReferenceNetworks.EvenNumbers() };
            List<Trial> trials = networks.Select(Trial.Generate).ToList();

            IReadOnlyList<TrialResult> exact = new ExhaustiveCpuEngine(maxConfigurations: 100_000).Run(trials, options, new Random(0));
            IReadOnlyList<TrialResult> sampled = MetalEngine.OrCpu(gpuThreshold: 0).Run(trials, options, new Random(0));

            for (int index = 0; index < trials.Count; index++)
            {
                Assert.True(exact[index].Exact);
                Assert.Subset(exact[index].Outputs.ToHashSet(), sampled[index].Outputs.ToHashSet());
                Assert.True(sampled[index].Outputs.Distinct().Count() >= 4, $"only {string.Join(",", sampled[index].Outputs.Distinct())}");
            }
        }

        [MetalFact]
        public void IsReproducibleFromTheSeed()
        {
            var options = new SimulationOptions(MaxSteps: 50, Repetitions: 50);
            Trial[] trials = { Trial.Generate(ReferenceNetworks.NaturalNumbers()), Trial.Generate(ReferenceNetworks.EvenNumbers()) };
            var engine = MetalEngine.OrCpu(gpuThreshold: 0);

            IReadOnlyList<TrialResult> first = engine.Run(trials, options, new Random(9));
            IReadOnlyList<TrialResult> second = engine.Run(trials, options, new Random(9));

            Assert.Equal(first.Select(result => result.Outputs), second.Select(result => result.Outputs));
        }
    }
}
