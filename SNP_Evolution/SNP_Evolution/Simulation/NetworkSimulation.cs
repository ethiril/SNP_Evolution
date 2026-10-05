using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly int[] legacyDelay;
        private readonly SpikeRelease[] legacyPending;
        private readonly int[] closedFor;
        private readonly long[] pendingEmission;
        private readonly long[] emitting;
        private readonly bool[] closed;
        private readonly int[] emitters;
        private readonly int[] matchingRules;
        private readonly List<int>? outputSpikeSteps;
        private readonly PortWatch? watch;
        // For a Ports readout: each neuron's slot in watch.Neurons or -1, whether it is a done neuron, and each slot's
        // firings so far.
        private readonly int[]? watchSlot;
        private readonly bool[]? isDone;
        private readonly List<Firing>[]? firings;
        private int? firstDoneStep;
        private int outputCounter;
        private bool outputEngaged;
        private int emitterCount;

        public NetworkSimulation(Network network, Random random)
            : this(CompiledNetwork.Of(network), random, InputSpikes.None, OutputTiming.Legacy)
        {
        }

        // recordSpikeTrain keeps every step the output neuron fires on, for OutputSpikeSteps. watch records the firings
        // a Ports readout needs, for PortRun.
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
            if (watch != null)
            {
                this.watch = watch;
                watchSlot = Enumerable.Repeat(-1, count).ToArray();
                isDone = new bool[count];
                firings = new List<Firing>[watch.Neurons.Count];
                for (int slot = 0; slot < watch.Neurons.Count; slot++)
                {
                    watchSlot[watch.Neurons[slot] - 1] = slot;
                    firings[slot] = new List<Firing>();
                }
                foreach (int position in watch.Done)
                {
                    isDone[position - 1] = true;
                }
            }
        }

        private NetworkSimulation(NetworkSimulation other)
            : this(other.network, null, other.input, other.timing, other.outputSpikeSteps != null, other.watch)
        {
            outputSpikeSteps?.AddRange(other.outputSpikeSteps!);
            for (int slot = 0; firings != null && slot < firings.Length; slot++)
            {
                firings[slot].AddRange(other.firings![slot]);
            }
            firstDoneStep = other.firstDoneStep;
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

        // Whether a Ports readout has seen all it waits for after done.
        public bool PortRunOver => firstDoneStep is int done && StepCount > done + watch!.StepsAfterDone;

        // What a Ports readout reads from this computation so far.
        public PortRun PortRun() =>
            new PortRun(firings!.Select(slot => (IReadOnlyList<Firing>)slot.ToArray()).ToArray(), (long[])spikes.Clone(), Array.AsReadOnly(network.initialSpikes));

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
            return firings == null ? state : state.Concat(PortHistory()).ToArray();
        }

        // Everything a Ports readout has recorded, flattened, so computations with different records are never merged.
        public long[] PortHistory()
        {
            var history = new List<long> { firstDoneStep ?? -1 };
            for (int slot = 0; slot < firings!.Length; slot++)
            {
                history.Add(-1 - slot);
                foreach (Firing firing in firings[slot])
                {
                    history.Add(firing.Step);
                    history.Add(firing.Spikes);
                }
            }
            return history.ToArray();
        }

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
            if (network.isOutput[neuron])
            {
                RecordOutputNeuron(release);
            }
            if (emitting[neuron] > 0)
            {
                emitters[emitterCount++] = neuron;
                if (watchSlot != null)
                {
                    RecordWatchedFiring(neuron);
                }
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

        private void RecordWatchedFiring(int neuron)
        {
            if (watchSlot![neuron] >= 0)
            {
                firings![watchSlot[neuron]].Add(new Firing(StepCount, emitting[neuron]));
            }
            if (isDone![neuron] && firstDoneStep == null)
            {
                firstDoneStep = StepCount;
            }
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
