using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Networks;

namespace SnpEvolution.Simulation
{
    // One sampled computation of a network: each step every neuron that can apply a rule applies one, chosen at random
    // among those that apply, through NetworkStep.
    //
    // With jitter j, what a neuron sends along each synapse arrives 0 to j steps late, drawn at random per synapse per
    // step, as on asynchronous hardware (see JitterBuffer).
    public sealed class NetworkSimulation
    {
        private readonly Random random;
        private readonly int[] matchingRules;
        private readonly int[] chosen;

        public NetworkSimulation(Network network, Random random)
            : this(CompiledNetwork.Of(network), random, InputSpikes.None, OutputTiming.Legacy)
        {
        }

        // recordSpikeTrain keeps every step the output neuron fires on, for OutputSpikeSteps, and watch the firings PortRun reports.
        public NetworkSimulation(CompiledNetwork network, Random random, InputSpikes input, OutputTiming timing, bool recordSpikeTrain = false,
            PortWatch? watch = null, int jitter = 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(jitter);
            this.random = random;
            matchingRules = new int[network.MaxRulesPerNeuron];
            chosen = new int[network.NeuronCount];
            Current = new NetworkStep(network, input, timing, recordSpikeTrain, watch, jitter > 0 ? new JitterBuffer(network.NeuronCount, jitter, random) : null);
        }

        // The computation so far.
        public NetworkStep Current { get; }

        public int StepCount => Current.StepCount;

        public int? Output => Current.Output;

        public IReadOnlyList<int> OutputSpikeSteps => Current.OutputSpikeSteps;

        public IReadOnlyList<long> Spikes => Current.Spikes;

        public bool IsHalted => Current.IsHalted;

        public bool PortRunOver => Current.PortRunOver;

        public PortRun PortRun() => Current.PortRun();

        public void Step()
        {
            // Choosing a neuron's rule reads only its own state, so every neuron chooses before any applies.
            for (int neuron = 0; neuron < chosen.Length; neuron++)
            {
                int matchingCount = Current.CollectApplicableRules(neuron, matchingRules);
                chosen[neuron] = matchingCount > 0 ? matchingRules[random.Next(matchingCount)] : NetworkStep.NoRule;
            }
            Current.Apply(chosen);
        }
    }
}
