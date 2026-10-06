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
    // closes the neuron for d steps: spikes sent to it are lost, and it emits when it reopens on step t + d. An axonal
    // rule consumes at once and leaves the neuron open, and its spikes leave on step t + d, as if the axon held them.
    public sealed class NetworkSimulation
    {
        public const int NoRule = -1;

        private readonly CompiledNetwork network;
        private readonly Random? random;
        private readonly InputSpikes input;
        private readonly OutputTiming timing;
        private readonly long[] spikes;
        private readonly int[] legacyDelay;
        private readonly SpikeRelease[] legacyPending;
        private readonly int[] closedFor;
        private readonly long[] pendingEmission;
        private readonly long[] emitting;
        private readonly bool[] closed;
        private readonly int[] emitters;
        private readonly int[] matchingRules;
        private readonly List<int>? outputSpikeSteps;
        private readonly PortRecorder? portRecorder;

        // Spikes on their way down each neuron's axon, in a ring of flightSlots per neuron indexed by step; null without axonal rules.
        private readonly long[]? inFlight;
        private readonly int flightSlots;
        private int outputCounter;
        private bool outputEngaged;
        private int emitterCount;

        public NetworkSimulation(Network network, Random random)
            : this(CompiledNetwork.Of(network), random, InputSpikes.None, OutputTiming.Legacy)
        {
        }

        // recordSpikeTrain keeps every step the output neuron fires on, for OutputSpikeSteps, and watch the firings PortRun reports.
        public NetworkSimulation(CompiledNetwork network, Random? random, InputSpikes input, OutputTiming timing, bool recordSpikeTrain = false,
            PortWatch? watch = null)
        {
            this.network = network;
            this.random = random;
            this.input = input;
            this.timing = timing;
            int count = network.NeuronCount;
            spikes = network.InitialSpikes.ToArray();
            legacyDelay = new int[count];
            legacyPending = new SpikeRelease[count];
            closedFor = new int[count];
            pendingEmission = new long[count];
            emitting = new long[count];
            closed = new bool[count];
            emitters = new int[count];
            matchingRules = new int[network.MaxRulesPerNeuron];
            outputSpikeSteps = recordSpikeTrain ? new List<int>() : null;
            portRecorder = watch == null ? null : new PortRecorder(watch, count);
            flightSlots = network.MaxAxonalDelay + 1;
            inFlight = network.MaxAxonalDelay > 0 ? new long[count * flightSlots] : null;
        }

        private NetworkSimulation(NetworkSimulation other)
            : this(other.network, null, other.input, other.timing, other.outputSpikeSteps != null)
        {
            outputSpikeSteps?.AddRange(other.outputSpikeSteps!);
            portRecorder = other.portRecorder?.Clone();
            Array.Copy(other.spikes, spikes, spikes.Length);
            Array.Copy(other.legacyDelay, legacyDelay, legacyDelay.Length);
            Array.Copy(other.legacyPending, legacyPending, legacyPending.Length);
            Array.Copy(other.closedFor, closedFor, closedFor.Length);
            Array.Copy(other.pendingEmission, pendingEmission, pendingEmission.Length);
            if (inFlight != null)
            {
                Array.Copy(other.inFlight!, inFlight, inFlight.Length);
            }
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

        // What each neuron sent along every synapse on the last step, spikes leaving its axon included.
        public IReadOnlyList<long> Sent => (long[])emitting.Clone();

        // Whether a Ports readout has seen all it waits for after done.
        public bool PortRunOver => portRecorder?.IsOver(StepCount) ?? false;

        // What a Ports readout reads from this computation so far.
        public PortRun PortRun() =>
            (portRecorder ?? throw new InvalidOperationException("This simulation watches no ports.")).Run((long[])spikes.Clone(), network.initialSpikes);

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
                return inFlight == null || Array.TrueForAll(inFlight, spikes => spikes == 0);
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
            // Choosing a neuron's rule reads only its own state, so each neuron chooses and applies in one pass.
            emitterCount = 0;
            for (int neuron = 0; neuron < network.NeuronCount; neuron++)
            {
                int matchingCount = CollectApplicableRules(neuron, matchingRules);
                Release(neuron, matchingCount > 0 ? matchingRules[random.Next(matchingCount)] : NoRule);
            }
            Deliver();
        }

        // Writes the rules the neuron could apply this step into the buffer and returns how many there are.
        public int CollectApplicableRules(int neuron, Span<int> buffer)
        {
            if (IsBusy(neuron))
            {
                return 0;
            }
            int count = 0;
            long held = spikes[neuron];
            int[] ruleStart = network.ruleStart;
            for (int rule = ruleStart[neuron], end = ruleStart[neuron + 1]; rule < end; rule++)
            {
                if (network.RuleApplies(rule, held))
                {
                    buffer[count++] = rule;
                }
            }
            return count;
        }

        // Steps with the given rule per neuron (NoRule for none), each of which must come from CollectApplicableRules.
        public void Apply(ReadOnlySpan<int> rules)
        {
            emitterCount = 0;
            for (int neuron = 0; neuron < network.NeuronCount; neuron++)
            {
                Release(neuron, rules[neuron]);
            }
            Deliver();
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
            if (inFlight != null)
            {
                // In arrival order from this step, so the same spikes in flight give the same state whatever the step.
                long[] flight = new long[inFlight.Length];
                for (int neuron = 0; neuron < count; neuron++)
                {
                    for (int ahead = 0; ahead < flightSlots; ahead++)
                    {
                        flight[neuron * flightSlots + ahead] = inFlight[FlightSlot(neuron, StepCount + ahead)];
                    }
                }
                state = [.. state, .. flight];
            }
            return portRecorder == null ? state : [.. state, .. portRecorder.History()];
        }

        private int FlightSlot(int neuron, int step) => neuron * flightSlots + step % flightSlots;

        // Everything a Ports readout has recorded, flattened, so computations with different records are never merged.
        public long[] PortHistory() => portRecorder?.History() ?? Array.Empty<long>();

        private int ApplicableRuleCount(int neuron)
        {
            Span<int> buffer = stackalloc int[network.MaxRulesPerNeuron];
            return CollectApplicableRules(neuron, buffer);
        }

        private bool IsBusy(int neuron) => legacyDelay[neuron] > 0 || closedFor[neuron] > 0;

        // Applies the neuron's part of this step with the chosen rule, and notes it as an emitter if it sends spikes.
        private void Release(int neuron, int rule)
        {
            SpikeRelease release = ReleaseSpikes(neuron, rule);
            if (inFlight != null && inFlight[FlightSlot(neuron, StepCount)] is long arriving and > 0)
            {
                // Spikes that have travelled the axon leave now, whatever the neuron itself does on this step.
                inFlight[FlightSlot(neuron, StepCount)] = 0;
                emitting[neuron] += arriving;
                release = SpikeRelease.Fired;
            }
            if (network.isOutput[neuron])
            {
                RecordOutputNeuron(release);
            }
            if (emitting[neuron] > 0)
            {
                emitters[emitterCount++] = neuron;
                portRecorder?.RecordFiring(neuron, StepCount, emitting[neuron]);
            }
        }

        // Sends what this step's emitters release, then the environment's input, once every neuron has released.
        private void Deliver()
        {
            int[] targetStart = network.targetStart;
            int[] targets = network.targets;
            for (int index = 0; index < emitterCount; index++)
            {
                int neuron = emitters[index];
                long sent = emitting[neuron];
                for (int target = targetStart[neuron], end = targetStart[neuron + 1]; target < end; target++)
                {
                    int receiver = targets[target];
                    if (!closed[receiver])
                    {
                        spikes[receiver] += sent;
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
            portRecorder?.NoteHeld(spikes);
            StepCount++;
        }

        // Applies the neuron's part of this step, setting what it emits and whether it is closed to incoming spikes.
        private SpikeRelease ReleaseSpikes(int neuron, int rule)
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
            if (network.ruleAxonal[rule])
            {
                spikes[neuron] = consume == CompiledNetwork.ConsumesAll ? 0 : spikes[neuron] - consume;
                inFlight![FlightSlot(neuron, StepCount + delay)] += network.RuleProduce[rule];
                return release == SpikeRelease.Fired ? SpikeRelease.None : release;
            }
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
