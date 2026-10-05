using System;
using System.Collections.Generic;
using SnpEvolution.Networks;

namespace SnpEvolution.Simulation
{
    public sealed class NetworkSimulation
    {
        private const int NoRule = -1;

        private readonly CompiledNetwork network;
        private readonly Random random;
        private readonly long[] spikes;
        private readonly int[] selectedRule;
        private readonly int[] remainingDelay;
        private readonly SpikeRelease[] pendingRelease;
        private readonly int[] matchingRules;
        private int outputCounter;
        private bool outputEngaged;

        public NetworkSimulation(Network network, Random random)
        {
            this.network = CompiledNetwork.Of(network);
            this.random = random;
            spikes = this.network.InitialSpikes.ToArray();
            selectedRule = new int[this.network.NeuronCount];
            remainingDelay = new int[this.network.NeuronCount];
            pendingRelease = new SpikeRelease[this.network.NeuronCount];
            matchingRules = new int[this.network.MaxRulesPerNeuron];
        }

        public int StepCount { get; private set; }

        // The output neuron's first spike starts the count and its second spike emits the number.
        public int? Output { get; private set; }

        public IReadOnlyList<long> Spikes => (long[])spikes.Clone();

        public void Step()
        {
            for (int neuron = 0; neuron < network.NeuronCount; neuron++)
            {
                SelectRule(neuron);
            }
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
                if (EmitsSpike(neuron))
                {
                    for (int target = network.TargetStart[neuron]; target < network.TargetStart[neuron + 1]; target++)
                    {
                        spikes[network.Targets[target]]++;
                    }
                }
            }
            StepCount++;
        }

        // A delayed rule still emits on the step it is selected; only the spike consumption waits.
        private bool EmitsSpike(int neuron) => selectedRule[neuron] != NoRule && network.RuleFires[selectedRule[neuron]];

        private void SelectRule(int neuron)
        {
            selectedRule[neuron] = NoRule;
            if (remainingDelay[neuron] > 0)
            {
                return;
            }
            int matchingCount = 0;
            for (int rule = network.RuleStart[neuron]; rule < network.RuleStart[neuron + 1]; rule++)
            {
                if (network.RuleMatches(rule, spikes[neuron]))
                {
                    matchingRules[matchingCount++] = rule;
                }
            }
            if (matchingCount > 0)
            {
                selectedRule[neuron] = matchingRules[random.Next(matchingCount)];
            }
        }

        private SpikeRelease ReleaseSpikes(int neuron)
        {
            if (remainingDelay[neuron] > 0)
            {
                remainingDelay[neuron]--;
                return SpikeRelease.None;
            }
            if (pendingRelease[neuron] != SpikeRelease.None)
            {
                SpikeRelease delayed = pendingRelease[neuron];
                pendingRelease[neuron] = SpikeRelease.None;
                spikes[neuron] = 0;
                return delayed;
            }
            int rule = selectedRule[neuron];
            if (rule == NoRule)
            {
                return SpikeRelease.None;
            }
            SpikeRelease release = network.RuleFires[rule] ? SpikeRelease.Fired : SpikeRelease.Forgot;
            if (network.RuleDelay[rule] > 0)
            {
                remainingDelay[neuron] = network.RuleDelay[rule];
                pendingRelease[neuron] = release;
                return SpikeRelease.None;
            }
            spikes[neuron] = 0;
            return release;
        }

        private void RecordOutputNeuron(SpikeRelease release)
        {
            if (release != SpikeRelease.Fired)
            {
                outputCounter++;
                return;
            }
            if (outputEngaged)
            {
                Output = ++outputCounter;
                return;
            }
            outputEngaged = true;
        }

        private enum SpikeRelease
        {
            None,
            Fired,
            Forgot,
        }
    }
}
