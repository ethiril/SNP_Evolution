using System;
using System.Collections.Generic;
using SnpEvolution.Networks;

namespace SnpEvolution.Simulation
{
    // How the output neuron's spikes become a number.
    public enum OutputTiming
    {
        // The original program's count: every step that is not an output spike, from the start until the second spike.
        Legacy,

        // The SN P definition: the number of steps between the output neuron's first and second spikes.
        Interval,
    }

    // One computation of a network. Each step every neuron that can apply a rule applies one, chosen at random among
    // those that apply; the exhaustive engine instead drives the choices itself through ApplicableRules and Apply.
    //
    // Legacy and standard rules keep their own delay semantics. A delayed legacy rule emits at once, then holds the
    // neuron for d steps, still receiving spikes, before emptying it. A delayed standard rule consumes at once and
    // closes the neuron for d steps: spikes sent to it are lost, and it emits when it reopens on step t + d.
    public sealed class NetworkSimulation
    {
        public const int NoRule = -1;

        private readonly CompiledNetwork network;
        private readonly Random? random;
        private readonly InputSpikes input;
        private readonly OutputTiming timing;
        private readonly long[] spikes;
        private readonly int[] selectedRule;
        private readonly int[] legacyDelay;
        private readonly SpikeRelease[] legacyPending;
        private readonly int[] closedFor;
        private readonly long[] pendingEmission;
        private readonly long[] emitting;
        private readonly bool[] closed;
        private readonly int[] matchingRules;
        private readonly List<int>? outputSpikeSteps;
        private int outputCounter;
        private bool outputEngaged;

        public NetworkSimulation(Network network, Random random)
            : this(CompiledNetwork.Of(network), random, InputSpikes.None, OutputTiming.Legacy)
        {
        }

        // recordSpikeTrain keeps every step the output neuron fires on, for OutputSpikeSteps.
        public NetworkSimulation(CompiledNetwork network, Random? random, InputSpikes input, OutputTiming timing, bool recordSpikeTrain = false)
        {
            this.network = network;
            this.random = random;
            this.input = input;
            this.timing = timing;
            int count = network.NeuronCount;
            spikes = network.InitialSpikes.ToArray();
            selectedRule = new int[count];
            legacyDelay = new int[count];
            legacyPending = new SpikeRelease[count];
            closedFor = new int[count];
            pendingEmission = new long[count];
            emitting = new long[count];
            closed = new bool[count];
            matchingRules = new int[network.MaxRulesPerNeuron];
            outputSpikeSteps = recordSpikeTrain ? new List<int>() : null;
        }

        private NetworkSimulation(NetworkSimulation other)
            : this(other.network, null, other.input, other.timing, other.outputSpikeSteps != null)
        {
            outputSpikeSteps?.AddRange(other.outputSpikeSteps!);
            Array.Copy(other.spikes, spikes, spikes.Length);
            Array.Copy(other.legacyDelay, legacyDelay, legacyDelay.Length);
            Array.Copy(other.legacyPending, legacyPending, legacyPending.Length);
            Array.Copy(other.closedFor, closedFor, closedFor.Length);
            Array.Copy(other.pendingEmission, pendingEmission, pendingEmission.Length);
            outputCounter = other.outputCounter;
            outputEngaged = other.outputEngaged;
            StepCount = other.StepCount;
            Output = other.Output;
        }

        public int StepCount { get; private set; }

        // Once every input spike has arrived, the step number no longer affects what happens next.
        public bool InputSpent => StepCount > input.LastStep;

        // Set once, by the output neuron's second spike.
        public int? Output { get; private set; }

        // The steps (from 0) the output neuron fired on, in order; empty unless the spike train is recorded.
        public IReadOnlyList<int> OutputSpikeSteps => outputSpikeSteps ?? (IReadOnlyList<int>)Array.Empty<int>();

        public IReadOnlyList<long> Spikes => (long[])spikes.Clone();

        public int NeuronCount => network.NeuronCount;

        public int MaxRulesPerNeuron => network.MaxRulesPerNeuron;

        // A halted network can never change again: nothing is applicable, nothing is pending and no input is left.
        public bool IsHalted
        {
            get
            {
                if (!InputSpent)
                {
                    return false;
                }
                for (int neuron = 0; neuron < network.NeuronCount; neuron++)
                {
                    if (IsBusy(neuron) || legacyPending[neuron] != SpikeRelease.None || ApplicableRuleCount(neuron) > 0)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        // A copy that the caller steps with Apply; it has no random of its own.
        public NetworkSimulation Clone() => new NetworkSimulation(this);

        public void Step()
        {
            if (random == null)
            {
                throw new InvalidOperationException("This simulation has no random to choose rules with; use Apply.");
            }
            for (int neuron = 0; neuron < network.NeuronCount; neuron++)
            {
                int matchingCount = CollectApplicableRules(neuron, matchingRules);
                selectedRule[neuron] = matchingCount > 0 ? matchingRules[random.Next(matchingCount)] : NoRule;
            }
            Advance();
        }

        // Writes the rules the neuron could apply this step into the buffer and returns how many there are.
        public int CollectApplicableRules(int neuron, Span<int> buffer)
        {
            if (IsBusy(neuron))
            {
                return 0;
            }
            int count = 0;
            for (int rule = network.RuleStart[neuron]; rule < network.RuleStart[neuron + 1]; rule++)
            {
                if (network.RuleApplies(rule, spikes[neuron]))
                {
                    buffer[count++] = rule;
                }
            }
            return count;
        }

        // Steps with the given rule per neuron (NoRule for none), each of which must come from CollectApplicableRules.
        public void Apply(ReadOnlySpan<int> rules)
        {
            rules.CopyTo(selectedRule);
            Advance();
        }

        // Everything that decides the future of the computation, so equal snapshots at the same step can be merged.
        // Without the output's progress, it is everything that decides whether the computation can halt.
        public long[] State(bool includeOutput = true)
        {
            int count = network.NeuronCount;
            var state = new long[5 * count + (includeOutput ? 3 : 0)];
            for (int neuron = 0; neuron < count; neuron++)
            {
                int offset = 5 * neuron;
                state[offset] = spikes[neuron];
                state[offset + 1] = legacyDelay[neuron];
                state[offset + 2] = (long)legacyPending[neuron];
                state[offset + 3] = closedFor[neuron];
                state[offset + 4] = pendingEmission[neuron];
            }
            if (includeOutput)
            {
                state[5 * count] = outputCounter;
                state[5 * count + 1] = outputEngaged ? 1 : 0;
                state[5 * count + 2] = Output ?? -1;
            }
            return state;
        }

        private int ApplicableRuleCount(int neuron)
        {
            Span<int> buffer = stackalloc int[network.MaxRulesPerNeuron];
            return CollectApplicableRules(neuron, buffer);
        }

        private bool IsBusy(int neuron) => legacyDelay[neuron] > 0 || closedFor[neuron] > 0;

        private void Advance()
        {
            for (int neuron = 0; neuron < network.NeuronCount; neuron++)
            {
                SpikeRelease release = ReleaseSpikes(neuron);
                if (network.IsOutput[neuron])
                {
                    RecordOutputNeuron(release);
                }
            }
            for (int neuron = 0; neuron < network.NeuronCount; neuron++)
            {
                if (emitting[neuron] == 0)
                {
                    continue;
                }
                for (int target = network.TargetStart[neuron]; target < network.TargetStart[neuron + 1]; target++)
                {
                    int receiver = network.Targets[target];
                    if (!closed[receiver])
                    {
                        spikes[receiver] += emitting[neuron];
                    }
                }
            }
            ReadOnlySpan<int> inputNeurons = network.InputNeurons;
            for (int index = 0; index < inputNeurons.Length && index < input.StepsPerInput.Count; index++)
            {
                if (!closed[inputNeurons[index]])
                {
                    spikes[inputNeurons[index]] += input.SpikesArriving(index, StepCount);
                }
            }
            StepCount++;
        }

        // Applies the neuron's part of this step, setting what it emits and whether it is closed to incoming spikes.
        private SpikeRelease ReleaseSpikes(int neuron)
        {
            emitting[neuron] = 0;
            closed[neuron] = false;
            if (legacyDelay[neuron] > 0)
            {
                legacyDelay[neuron]--;
                return SpikeRelease.None;
            }
            if (closedFor[neuron] > 0)
            {
                if (--closedFor[neuron] > 0)
                {
                    closed[neuron] = true;
                    return SpikeRelease.None;
                }
                emitting[neuron] = pendingEmission[neuron];
                pendingEmission[neuron] = 0;
                return emitting[neuron] > 0 ? SpikeRelease.Fired : SpikeRelease.Forgot;
            }
            int rule = selectedRule[neuron];
            if (legacyPending[neuron] != SpikeRelease.None)
            {
                // The original program let a rule chosen on this step emit, while the delayed rule emptied the neuron.
                emitting[neuron] = rule == NoRule ? 0 : network.RuleProduce[rule];
                SpikeRelease delayed = legacyPending[neuron];
                legacyPending[neuron] = SpikeRelease.None;
                spikes[neuron] = 0;
                return delayed;
            }
            if (rule == NoRule)
            {
                return SpikeRelease.None;
            }
            SpikeRelease release = network.RuleFires[rule] ? SpikeRelease.Fired : SpikeRelease.Forgot;
            int delay = network.RuleDelay[rule];
            long consume = network.RuleConsume[rule];
            if (consume == CompiledNetwork.ConsumesAll)
            {
                emitting[neuron] = network.RuleProduce[rule];
                if (delay > 0)
                {
                    legacyDelay[neuron] = delay;
                    legacyPending[neuron] = release;
                    return SpikeRelease.None;
                }
                spikes[neuron] = 0;
                return release;
            }
            spikes[neuron] -= consume;
            if (delay > 0)
            {
                closedFor[neuron] = delay;
                pendingEmission[neuron] = network.RuleProduce[rule];
                closed[neuron] = true;
                return SpikeRelease.None;
            }
            emitting[neuron] = network.RuleProduce[rule];
            return release;
        }

        private void RecordOutputNeuron(SpikeRelease release)
        {
            if (release == SpikeRelease.Fired)
            {
                outputSpikeSteps?.Add(StepCount);
            }
            if (Output != null)
            {
                return;
            }
            if (release != SpikeRelease.Fired)
            {
                if (outputEngaged || timing == OutputTiming.Legacy)
                {
                    outputCounter++;
                }
                return;
            }
            if (outputEngaged)
            {
                Output = ++outputCounter;
                return;
            }
            outputEngaged = true;
        }

        private enum SpikeRelease : byte
        {
            None,
            Fired,
            Forgot,
        }
    }
}
