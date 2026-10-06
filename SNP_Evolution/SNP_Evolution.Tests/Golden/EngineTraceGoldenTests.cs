using System.Text;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Export;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;
using SnpEvolution.Tests.Simulation;

namespace SnpEvolution.Tests.Golden
{
    // What each engine makes of the reference networks and of every part in parts/, parts-profile/ and the hand-built
    // library: every neuron's firings, step by step, in every run or computation, so a change to the step semantics in
    // any engine shows here. The exporters' own step (SpikeTrace) is pinned the same way.
    public class EngineTraceGoldenTests
    {
        private const int Repetitions = 3;
        private const int ReferenceSteps = 12;

        public static TheoryData<string> Engines => new TheoryData<string> { "sequential", "parallel", "exhaustive" };

        [Theory]
        [MemberData(nameof(Engines))]
        public void EveryEngineRunsTheReferenceNetworksAndPartsAsBefore(string engine)
        {
            GoldenFile.Check($"engine-{engine}", Traces(Create(engine)));
        }

        // Metal hands Ports readouts and batches this small to the parallel engine with the same random, so it should
        // trace exactly as that engine's golden file does.
        [MetalFact]
        public void TheGpuEngineTracesAsTheParallelEngineDoes()
        {
            Assert.Equal(Traces(new ParallelCpuEngine()), Traces(new MetalEngine()));
        }

        [Fact]
        public void TheExportersStepRunsEveryPartAsBefore()
        {
            var text = new StringBuilder();
            foreach ((string name, Part part) in Parts())
            {
                text.Append("== ").Append(name).Append('\n');
                if (SpikeTrace.Choices(part.Network).FirstOrDefault() is string choice)
                {
                    text.Append(choice).Append('\n');
                    continue;
                }
                foreach ((string label, InputSpikes input, int steps) in SpikeTrace.Cases(part.Contract))
                {
                    text.Append(label).Append('\n');
                    // held/sent/after for each neuron, sent shown as - when it applied no rule; a run of identical steps is one line.
                    List<string> rows = SpikeTrace.Run(part.Network, input, steps).Steps
                        .Select(row => string.Join(' ', row.Select(neuron => $"{neuron.Held}/{(neuron.Applied ? neuron.Sent.ToString() : "-")}/{neuron.After}"))).ToList();
                    int first = 0;
                    while (first < rows.Count)
                    {
                        int last = first;
                        while (last + 1 < rows.Count && rows[last + 1] == rows[first])
                        {
                            last++;
                        }
                        text.Append(first == last ? $"{first}" : $"{first}-{last}").Append(": ").Append(rows[first]).Append('\n');
                        first = last + 1;
                    }
                }
            }
            GoldenFile.Check("exporter-steps", text.ToString());
        }

        private static ISimulationEngine Create(string engine) => engine switch
        {
            "sequential" => new SequentialCpuEngine(),
            "parallel" => new ParallelCpuEngine(),
            _ => new ExhaustiveCpuEngine(),
        };

        private static IEnumerable<(string Name, Part Part)> Parts() =>
            new[] { "parts", "parts-profile" }
                .SelectMany(folder => Directory.GetFiles(Path.Combine(RepositoryFiles.Root, folder), "*.json").Select(Path.GetFileName).Order(StringComparer.Ordinal)
                    .Select(file => ($"{folder}/{file}", RepositoryFiles.ReadPart(folder, file!).Part)))
                .Concat(HandBuiltParts.All().Select(part => ($"hand-built {part.Contract.Name}", part)));

        private static string Traces(ISimulationEngine engine)
        {
            var text = new StringBuilder();
            foreach ((string name, Network network) in new[] { ("natural numbers", ReferenceNetworks.NaturalNumbers()), ("even numbers", ReferenceNetworks.EvenNumbers()) })
            {
                var watch = new PortWatch(EveryNeuron(network), Array.Empty<int>(), 0);
                var trials = new[] { Readout.Output, Readout.SpikeTrain, Readout.Ports }.Select(readout => new Trial(network, InputSpikes.None, readout, watch)).ToList();
                IReadOnlyList<TrialResult> results = engine.Run(trials, new SimulationOptions(ReferenceSteps, Repetitions), new Random(1));
                text.Append("== reference ").Append(name).Append('\n');
                for (int index = 0; index < trials.Count; index++)
                {
                    text.Append(trials[index].Readout).Append('\n');
                    Append(text, results[index]);
                }
            }
            foreach ((string name, Part part) in Parts())
            {
                ContractTask task = part.Task();
                List<Trial> trials = task.Cases.Select(@case => new Trial(part.Network, @case.Input, Readout.Ports, @case.Watch! with { Neurons = EveryNeuron(part.Network) })).ToList();
                IReadOnlyList<TrialResult> results = engine.Run(trials, new SimulationOptions(task.StepsNeeded, Repetitions, OutputTiming.Interval), new Random(1));
                text.Append("== ").Append(name).Append('\n');
                foreach (((string label, _, _), TrialResult result) in SpikeTrace.Cases(part.Contract).Zip(results))
                {
                    text.Append(label).Append('\n');
                    Append(text, result);
                }
            }
            return text.ToString();
        }

        private static List<int> EveryNeuron(Network network) => Enumerable.Range(1, network.Neurons.Count).ToList();

        // Firings as step, or stepxspikes when a neuron sends more than one.
        private static void Append(StringBuilder text, TrialResult result)
        {
            text.Append($"outputs [{string.Join(",", result.Outputs)}] halts {result.CanHalt} exact {result.Exact}\n");
            foreach (IReadOnlyList<int> train in result.SpikeTrains)
            {
                text.Append("train ").AppendJoin(',', train).Append('\n');
            }
            foreach (PortRun run in result.PortRuns)
            {
                text.Append("run").AppendJoin("", run.Firings.Select((firings, neuron) =>
                    $" n{neuron + 1}:" + string.Join(",", firings.Select(firing => firing.Spikes == 1 ? $"{firing.Step}" : $"{firing.Step}x{firing.Spikes}"))));
                text.Append($" start [{string.Join(",", run.InitialSpikes)}] end [{string.Join(",", run.FinalSpikes)}] most {run.MostHeld}\n");
            }
        }
    }
}
