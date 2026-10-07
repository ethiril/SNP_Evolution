using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SnpEvolution.Simulation
{
    // Follows every computation instead of sampling some, so the outputs are exactly the set the network can produce
    // within MaxSteps and a score cannot be lucky. Computations that reach the same configuration on the same step are
    // merged, which keeps the small networks evolution works with cheap. When only halting matters and the input is
    // spent, a configuration seen on any earlier step is dropped too, as its future has already been explored with
    // more steps to spare; a network that cycles without halting is then settled in a few steps. A trial whose
    // configurations outgrow maxConfigurations is sampled instead, and its result says TooWide; so is one whose
    // successors on a step, merged or not, outgrow WorkPerConfiguration times that, since each is built before it can
    // be merged and a few wide neurons can make millions that merge into a few thousand. Spike train
    // and Ports readouts keep what they have recorded in the merge key, so only computations with the same record so far
    // are merged; each distinct computation is reported once. A deterministic network then costs one configuration a step.
    // Jitter would need a delay choice for every spike on every synapse, past following, so it is not supported.
    public sealed class ExhaustiveCpuEngine : ISimulationEngine
    {
        public const int DefaultMaxConfigurations = 2_000;

        // How many successors a step may build for each configuration it may keep.
        public const int WorkPerConfiguration = 16;

        private readonly int maxConfigurations;

        public ExhaustiveCpuEngine(int maxConfigurations = DefaultMaxConfigurations)
        {
            this.maxConfigurations = maxConfigurations;
        }

        public EngineSupport Support { get; } = new EngineSupport(Jitter: false, AxonalDelay: true, Ports: true, EveryComputation: true);

        public IReadOnlyList<TrialResult> Run(IReadOnlyList<Trial> trials, SimulationOptions options, Random random)
        {
            int[] seeds = Sampling.Seeds(trials.Count, random);
            var results = new TrialResult[trials.Count];
            Parallel.For(0, trials.Count, index =>
            {
                TrialResult explored = Explore(trials[index], options);
                results[index] = explored.Coverage == TrialCoverage.TooWide
                    ? NetworkRunner.Sample(trials[index], options, new Random(seeds[index])) with { Coverage = TrialCoverage.TooWide }
                    : explored;
            });
            return results;
        }

        // Every computation of the trial; a TooWide result, with nothing in it, when there are too many to follow.
        public TrialResult Explore(Trial trial, SimulationOptions options)
        {
            if (!Support.Runs(trial, options))
            {
                return TrialResult.Unsupported;
            }
            var outputs = new SortedSet<int>();
            var portRuns = new Dictionary<long[], PortRun>(StateComparer.Instance);
            var spikeTrains = new Dictionary<long[], IReadOnlyList<int>>(StateComparer.Instance);
            bool recordSpikeTrain = trial.Readout == Readout.SpikeTrain;
            bool canHalt = false;
            PortWatch? watch = trial.PortsWatch;
            var frontier = new List<NetworkStep> { new NetworkStep(CompiledNetwork.Of(trial.Network), trial.Input, options.Timing, recordSpikeTrain, watch) };
            HashSet<long[]>? seen = trial.Readout == Readout.Halting ? new HashSet<long[]>(StateComparer.Instance) : null;
            for (int step = 0; step < options.MaxSteps && frontier.Count > 0; step++)
            {
                var next = new Dictionary<long[], NetworkStep>(StateComparer.Instance);
                long work = 0;
                foreach (NetworkStep configuration in frontier)
                {
                    if (configuration.IsHalted)
                    {
                        // One halting computation is all an acceptor needs, so there is no point exploring further.
                        if (trial.Readout == Readout.Halting)
                        {
                            return new TrialResult(Array.Empty<int>(), true, TrialCoverage.Exact);
                        }
                        canHalt = true;
                        Finish(configuration);
                        continue;
                    }
                    if (watch != null && configuration.PortRunOver)
                    {
                        AddPortRun(portRuns, configuration);
                        continue;
                    }
                    if (!Expand(configuration, trial.Readout, outputs, next, seen, ref work))
                    {
                        return new TrialResult(Array.Empty<int>(), false, TrialCoverage.TooWide);
                    }
                }
                frontier = next.Values.ToList();
            }
            canHalt |= frontier.Any(configuration => configuration.IsHalted);
            frontier.ForEach(Finish);
            return new TrialResult(outputs.ToList(), canHalt, TrialCoverage.Exact, spikeTrains.Values.ToList(), portRuns.Values.ToList());

            // Records a computation that has run its course, once for each distinct record.
            void Finish(NetworkStep configuration)
            {
                if (watch != null)
                {
                    AddPortRun(portRuns, configuration);
                }
                if (recordSpikeTrain)
                {
                    spikeTrains.TryAdd(configuration.OutputSpikeSteps.Select(step => (long)step).ToArray(), configuration.OutputSpikeSteps);
                    if (configuration.Output is int output)
                    {
                        outputs.Add(output);
                    }
                }
            }
        }

        private static void AddPortRun(Dictionary<long[], PortRun> portRuns, NetworkStep configuration) =>
            portRuns.TryAdd(configuration.PortHistory().Concat(configuration.Spikes).ToArray(), configuration.PortRun());

        // Adds every successor of the configuration to next, or returns false once there are too many, kept or built
        // this step; work counts the successors the step has built so far.
        private bool Expand(
            NetworkStep configuration, Readout readout, SortedSet<int> outputs, Dictionary<long[], NetworkStep> next, HashSet<long[]>? seen, ref long work)
        {
            int neuronCount = configuration.NeuronCount;
            var options = new int[neuronCount][];
            Span<int> buffer = new int[configuration.MaxRulesPerNeuron];
            long combinations = 1;
            for (int neuron = 0; neuron < neuronCount; neuron++)
            {
                int count = configuration.CollectApplicableRules(neuron, buffer);
                options[neuron] = count == 0 ? new[] { NetworkStep.NoRule } : buffer[..count].ToArray();
                combinations *= options[neuron].Length;
                if (combinations > maxConfigurations)
                {
                    return false;
                }
            }
            work += combinations;
            if (work > (long)maxConfigurations * WorkPerConfiguration)
            {
                return false;
            }
            var choice = new int[neuronCount];
            var position = new int[neuronCount];
            for (long combination = 0; combination < combinations; combination++)
            {
                for (int neuron = 0; neuron < neuronCount; neuron++)
                {
                    choice[neuron] = options[neuron][position[neuron]];
                }
                NetworkStep successor = configuration.Clone();
                successor.Apply(choice);
                if (readout == Readout.Output && successor.Output is int output)
                {
                    outputs.Add(output);
                }
                else
                {
                    if (seen != null && successor.InputSpent)
                    {
                        long[] haltingState = successor.State(includeOutput: false);
                        if (!seen.Add(haltingState))
                        {
                            continue;
                        }
                        next.TryAdd(haltingState, successor);
                    }
                    else
                    {
                        next.TryAdd(successor.State(), successor);
                    }
                    if (next.Count > maxConfigurations)
                    {
                        return false;
                    }
                }
                for (int neuron = 0; neuron < neuronCount && ++position[neuron] == options[neuron].Length; neuron++)
                {
                    position[neuron] = 0;
                }
            }
            return true;
        }

        private sealed class StateComparer : IEqualityComparer<long[]>
        {
            public static readonly StateComparer Instance = new StateComparer();

            public bool Equals(long[]? first, long[]? second) => first.AsSpan().SequenceEqual(second);

            public int GetHashCode(long[] state)
            {
                var hash = new HashCode();
                foreach (long value in state)
                {
                    hash.Add(value);
                }
                return hash.ToHashCode();
            }
        }
    }
}
