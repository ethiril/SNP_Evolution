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
    // configurations outgrow maxConfigurations falls back to sampling, and its result says it is not exact. Spike train
    // and Ports readouts keep what they have recorded in the merge key, so only computations with the same record so far
    // are merged; each distinct computation is reported once. A deterministic network then costs one configuration a step.
    public sealed class ExhaustiveCpuEngine : ISimulationEngine
    {
        public const int DefaultMaxConfigurations = 2_000;

        private readonly int maxConfigurations;

        public ExhaustiveCpuEngine(int maxConfigurations = DefaultMaxConfigurations)
        {
            this.maxConfigurations = maxConfigurations;
        }

        // Refuses jitter: a delay choice for every spike on every synapse would multiply the computations past following.
        public IReadOnlyList<TrialResult> Run(IReadOnlyList<Trial> trials, SimulationOptions options, Random random)
        {
            if (options.Jitter > 0)
            {
                throw new ArgumentException("The exhaustive engine cannot follow jitter; sample jittered runs instead.", nameof(options));
            }
            int[] seeds = ParallelCpuEngine.Seeds(trials.Count, random);
            var results = new TrialResult[trials.Count];
            Parallel.For(0, trials.Count, index =>
                results[index] = Explore(trials[index], options) ?? NetworkRunner.Sample(trials[index], options, new Random(seeds[index])));
            return results;
        }

        // Null when the computation tree is too wide to follow.
        public TrialResult? Explore(Trial trial, SimulationOptions options)
        {
            if (options.Jitter > 0)
            {
                throw new ArgumentException("The exhaustive engine cannot follow jitter; sample jittered runs instead.", nameof(options));
            }
            var outputs = new SortedSet<int>();
            var portRuns = new Dictionary<long[], PortRun>(StateComparer.Instance);
            var spikeTrains = new Dictionary<long[], IReadOnlyList<int>>(StateComparer.Instance);
            bool recordSpikeTrain = trial.Readout == Readout.SpikeTrain;
            bool canHalt = false;
            PortWatch? watch = NetworkRunner.WatchOf(trial);
            var frontier = new List<NetworkSimulation> { new NetworkSimulation(CompiledNetwork.Of(trial.Network), null, trial.Input, options.Timing, recordSpikeTrain, watch) };
            HashSet<long[]>? seen = trial.Readout == Readout.Halting ? new HashSet<long[]>(StateComparer.Instance) : null;
            for (int step = 0; step < options.MaxSteps && frontier.Count > 0; step++)
            {
                var next = new Dictionary<long[], NetworkSimulation>(StateComparer.Instance);
                foreach (NetworkSimulation configuration in frontier)
                {
                    if (configuration.IsHalted)
                    {
                        // One halting computation is all an acceptor needs, so there is no point exploring further.
                        if (trial.Readout == Readout.Halting)
                        {
                            return new TrialResult(Array.Empty<int>(), true, Exact: true);
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
                    if (!Expand(configuration, trial.Readout, outputs, next, seen))
                    {
                        return null;
                    }
                }
                frontier = next.Values.ToList();
            }
            canHalt |= frontier.Any(configuration => configuration.IsHalted);
            frontier.ForEach(Finish);
            return new TrialResult(outputs.ToList(), canHalt, Exact: true, spikeTrains.Values.ToList(), portRuns.Values.ToList());

            // Records a computation that has run its course, once for each distinct record.
            void Finish(NetworkSimulation configuration)
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

        private static void AddPortRun(Dictionary<long[], PortRun> portRuns, NetworkSimulation configuration) =>
            portRuns.TryAdd(configuration.PortHistory().Concat(configuration.Spikes).ToArray(), configuration.PortRun());

        // Adds every successor of the configuration to next, or returns false once there are too many.
        private bool Expand(
            NetworkSimulation configuration, Readout readout, SortedSet<int> outputs, Dictionary<long[], NetworkSimulation> next, HashSet<long[]>? seen)
        {
            int neuronCount = configuration.NeuronCount;
            var options = new int[neuronCount][];
            Span<int> buffer = new int[configuration.MaxRulesPerNeuron];
            long combinations = 1;
            for (int neuron = 0; neuron < neuronCount; neuron++)
            {
                int count = configuration.CollectApplicableRules(neuron, buffer);
                options[neuron] = count == 0 ? new[] { NetworkSimulation.NoRule } : buffer[..count].ToArray();
                combinations *= options[neuron].Length;
                if (combinations > maxConfigurations)
                {
                    return false;
                }
            }
            var choice = new int[neuronCount];
            var position = new int[neuronCount];
            for (long combination = 0; combination < combinations; combination++)
            {
                for (int neuron = 0; neuron < neuronCount; neuron++)
                {
                    choice[neuron] = options[neuron][position[neuron]];
                }
                NetworkSimulation successor = configuration.Clone();
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
