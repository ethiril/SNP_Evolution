using System.Text;
using SnpEvolution.Model;
using SnpEvolution.Simulation;
using SnpEvolution.Simulation.Metal;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;
using SnpEvolution.Specs.Verification;

namespace SnpEvolution.Tests.Golden
{
    // Every neuron's firings, step by step, so a change to any engine's step semantics shows in a golden file.
    public class EngineTraceGoldenTests
    {
        private const int Repetitions = 3;
        private const int ReferenceSteps = 12;

        public static TheoryData<string> EngineNames => new TheoryData<string> { "sequential", "parallel", "exhaustive" };

        [Theory]
        [MemberData(nameof(EngineNames))]
        public void EveryEngineRunsTheReferenceNetworksAndPartsAsBefore(string engine)
        {
            GoldenFile.Check($"engine-{engine}", Traces(Create(engine)));
        }

        // Metal hands Ports readouts and batches this small to the parallel engine with the same random, so it should
        // trace exactly as that engine's golden file does.
        [MetalFact]
        public void TheGpuEngineTracesAsTheParallelEngineDoes()
        {
            Assert.Equal(Traces(new ParallelCpuEngine()), Traces(MetalEngine.OrCpu()));
        }

        private static ISimulationEngine Create(string engine) => engine switch
        {
            "sequential" => new SequentialCpuEngine(),
            "parallel" => new ParallelCpuEngine(),
            _ => new ExhaustiveCpuEngine(),
        };

        private static string Traces(ISimulationEngine engine)
        {
            var text = new StringBuilder();
            AppendReferenceTraces(text, engine);
            AppendPartTraces(text, engine);
            return text.ToString();
        }

        private static void AppendReferenceTraces(StringBuilder text, ISimulationEngine engine)
        {
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
        }

        private static void AppendPartTraces(StringBuilder text, ISimulationEngine engine)
        {
            foreach ((string name, Part part) in GoldenParts.All())
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
