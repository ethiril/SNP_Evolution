using System;
using System.Collections.Generic;
using SnpEvolution.Networks;

namespace SnpEvolution.Simulation
{
    // One computation of a network, stepped by whoever chooses the rules: each step every neuron applies the rule it is
    // given (NoRule for none), releasing its spikes, and then what was released is delivered with the environment's
    // input. NetworkSimulation chooses at random; the exhaustive engine follows every choice; SpikeTrace takes the only one.
    //
    // What a delay does follows the rule's DelayKind: holding, closing or axonal. With jitter (see JitterBuffer), what a
    // neuron sends along each synapse may arrive late, and a late arrival is lost if its receiver is closed when it lands.
    // Input from the environment is never late.
    public sealed class NetworkStep
    {
        public const int NoRule = -1;

        private readonly CompiledNetwork network;
        private readonly InputSpikes input;
        private readonly NeuronStates neurons;
        private readonly OutputDecoder decoder;
        private readonly PortRecorder? portRecorder;
        private readonly AxonBuffer? axon;
        private readonly JitterBuffer? jitter;
        private readonly long[] emitting;
        private readonly bool[] closed;
        private readonly int[] emitters;
        private int emitterCount;

        // recordSpikeTrain keeps every step the output neuron fires on, for OutputSpikeSteps, and watch the firings PortRun reports.
        public NetworkStep(CompiledNetwork network, InputSpikes input, OutputTiming timing, bool recordSpikeTrain = false, PortWatch? watch = null)
            : this(network, input, timing, recordSpikeTrain, watch, jitter: null)
        {
        }

        internal NetworkStep(CompiledNetwork network, InputSpikes input, OutputTiming timing, bool recordSpikeTrain, PortWatch? watch, JitterBuffer? jitter)
        {
            this.network = network;
            this.input = input;
            this.jitter = jitter;
            int count = network.NeuronCount;
            neurons = new NeuronStates(network.InitialSpikes);
            decoder = new OutputDecoder(timing, recordSpikeTrain);
            portRecorder = watch == null ? null : new PortRecorder(watch, count);
            axon = network.MaxAxonalDelay > 0 ? new AxonBuffer(count, network.MaxAxonalDelay) : null;
            emitting = new long[count];
            closed = new bool[count];
            emitters = new int[count];
        }

        private NetworkStep(NetworkStep other)
        {
            network = other.network;
            input = other.input;
            neurons = new NeuronStates(other.neurons);
            decoder = new OutputDecoder(other.decoder);
            portRecorder = other.portRecorder?.Clone();
            axon = other.axon == null ? null : new AxonBuffer(other.axon);
            emitting = new long[network.NeuronCount];
            closed = new bool[network.NeuronCount];
            emitters = new int[network.NeuronCount];
            StepCount = other.StepCount;
        }

        public int StepCount { get; private set; }

        // Once every input spike has arrived, the step number no longer affects what happens next.
        public bool InputSpent => StepCount > input.LastStep;

        // Set once, by the output neuron's second spike.
        public int? Output => decoder.Output;

        // The steps (from 0) the output neuron fired on, in order; empty unless the spike train is recorded.
        public IReadOnlyList<int> OutputSpikeSteps => decoder.SpikeSteps ?? Array.Empty<int>();

        public IReadOnlyList<long> Spikes => (long[])neurons.Spikes.Clone();

        // What each neuron sent along every synapse on the last step, spikes leaving its axon included.
        public IReadOnlyList<long> Sent => (long[])emitting.Clone();

        // Whether a Ports readout has seen all it waits for after done.
        public bool PortRunOver => portRecorder?.IsOver(StepCount) ?? false;

        public int NeuronCount => network.NeuronCount;

        public int MaxRulesPerNeuron => network.MaxRulesPerNeuron;

        // What a Ports readout reads from this computation so far.
        // Spikes still held back by jitter count as their receivers' own, so a part is not back to start while any are on their way.
        public PortRun PortRun()
        {
            long[] held = (long[])neurons.Spikes.Clone();
            jitter?.AddHeldTo(held);
            return (portRecorder ?? throw new InvalidOperationException("This computation watches no ports.")).Run(held, network.initialSpikes);
        }

        // Everything a Ports readout has recorded, flattened, so computations with different records are never merged.
        public long[] PortHistory() => portRecorder?.History() ?? Array.Empty<long>();

        // A halted network can never change again: nothing is applicable, nothing is pending and no input is left.
        public bool IsHalted
        {
            get
            {
                if (!InputSpent)
                {
                    return false;
                }
                Span<int> buffer = stackalloc int[network.MaxRulesPerNeuron];
                for (int neuron = 0; neuron < network.NeuronCount; neuron++)
                {
                    if (neurons.IsBusy(neuron) || neurons.LegacyPending[neuron] != SpikeRelease.None || CollectApplicableRules(neuron, buffer) > 0)
                    {
                        return false;
                    }
                }
                return (axon?.IsEmpty ?? true) && (jitter?.IsEmpty ?? true);
            }
        }

        // A copy to step separately. A jittered computation draws its delays from its random and cannot be copied.
        public NetworkStep Clone() =>
            jitter == null ? new NetworkStep(this) : throw new InvalidOperationException("A jittered computation draws its delays at random and cannot be cloned.");

        // See StateKey; includeOutput false leaves out the output's progress.
        public long[] State(bool includeOutput = true) =>
            StateKey.Of(neurons, includeOutput ? decoder : null, axon, StepCount, decoder.SpikeSteps, portRecorder);

        // Writes the rules the neuron could apply this step into the buffer and returns how many there are.
        public int CollectApplicableRules(int neuron, Span<int> buffer)
        {
            if (neurons.IsBusy(neuron))
            {
                return 0;
            }
            int count = 0;
            long held = neurons.Spikes[neuron];
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

        // Applies the neuron's part of this step with the chosen rule, and notes it as an emitter if it sends spikes.
        private void Release(int neuron, int rule)
        {
            SpikeRelease release = ReleaseSpikes(neuron, rule);
            if (axon?.Leave(neuron, StepCount) is long leaving and > 0)
            {
                // Spikes that have travelled the axon leave now, whatever the neuron itself does on this step.
                emitting[neuron] += leaving;
                release = SpikeRelease.Fired;
            }
            if (network.isOutput[neuron])
            {
                decoder.Record(release, StepCount);
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
            long[] spikes = neurons.Spikes;
            int[] targetStart = network.targetStart;
            int[] targets = network.targets;
            for (int index = 0; index < emitterCount; index++)
            {
                int neuron = emitters[index];
                long sent = emitting[neuron];
                for (int target = targetStart[neuron], end = targetStart[neuron + 1]; target < end; target++)
                {
                    int receiver = targets[target];
                    int lateBy = jitter?.DrawLateness() ?? 0;
                    if (lateBy > 0)
                    {
                        jitter!.Hold(receiver, StepCount + lateBy, sent);
                    }
                    else if (!closed[receiver])
                    {
                        spikes[receiver] += sent;
                    }
                }
            }
            if (jitter != null)
            {
                for (int receiver = 0; receiver < network.NeuronCount; receiver++)
                {
                    long arriving = jitter.Arrive(receiver, StepCount);
                    if (!closed[receiver])
                    {
                        spikes[receiver] += arriving;
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
            long[] spikes = neurons.Spikes;
            emitting[neuron] = 0;
            closed[neuron] = false;
            if (neurons.LegacyDelay[neuron] > 0)
            {
                neurons.LegacyDelay[neuron]--;
                return SpikeRelease.None;
            }
            if (neurons.ClosedFor[neuron] > 0)
            {
                if (--neurons.ClosedFor[neuron] > 0)
                {
                    closed[neuron] = true;
                    return SpikeRelease.None;
                }
                emitting[neuron] = neurons.PendingEmission[neuron];
                neurons.PendingEmission[neuron] = 0;
                return emitting[neuron] > 0 ? SpikeRelease.Fired : SpikeRelease.Forgot;
            }
            if (neurons.LegacyPending[neuron] != SpikeRelease.None)
            {
                // The original program let a rule chosen on this step emit, while the delayed rule emptied the neuron.
                emitting[neuron] = rule == NoRule ? 0 : network.RuleProduce[rule];
                SpikeRelease delayed = neurons.LegacyPending[neuron];
                neurons.LegacyPending[neuron] = SpikeRelease.None;
                spikes[neuron] = 0;
                return delayed;
            }
            if (rule == NoRule)
            {
                return SpikeRelease.None;
            }
            SpikeRelease release = network.RuleFires[rule] ? SpikeRelease.Fired : SpikeRelease.Forgot;
            int delay = network.RuleDelay[rule];
            int sends = network.RuleProduce[rule];
            switch (network.ruleDelayKind[rule])
            {
                case DelayKind.Axonal:
                    spikes[neuron] = network.Rule(rule).Leaves(spikes[neuron]);
                    axon!.Send(neuron, StepCount + delay, sends);
                    return release == SpikeRelease.Fired ? SpikeRelease.None : release;
                case DelayKind.Holding:
                    emitting[neuron] = sends;
                    neurons.LegacyDelay[neuron] = delay;
                    neurons.LegacyPending[neuron] = release;
                    return SpikeRelease.None;
                case DelayKind.Closing:
                    spikes[neuron] = network.Rule(rule).Leaves(spikes[neuron]);
                    neurons.ClosedFor[neuron] = delay;
                    neurons.PendingEmission[neuron] = sends;
                    closed[neuron] = true;
                    return SpikeRelease.None;
                default:
                    spikes[neuron] = network.Rule(rule).Leaves(spikes[neuron]);
                    emitting[neuron] = sends;
                    return release;
            }
        }
    }
}
