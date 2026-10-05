using System;
using System.Collections.Generic;
using System.Linq;

namespace SnpEvolution.Networks
{
    public sealed class NetworkSimulation
    {
        private readonly Random random;
        private readonly NeuronState[] states;
        private int outputCounter;
        private bool outputEngaged;

        public NetworkSimulation(Network network, Random random)
        {
            this.random = random;
            states = network.Neurons.Select(neuron => new NeuronState(neuron)).ToArray();
        }

        public int StepCount { get; private set; }

        // The output neuron's first spike starts the count and its second spike emits the number.
        public int? Output { get; private set; }

        public IReadOnlyList<string> Spikes => states.Select(state => state.Spikes).ToList();

        public void Step()
        {
            foreach (NeuronState state in states)
            {
                state.SelectRule(random);
            }
            foreach (NeuronState state in states)
            {
                SpikeRelease release = state.ReleaseSpikes();
                if (state.Neuron.IsOutput)
                {
                    RecordOutputNeuron(release);
                }
            }
            foreach (NeuronState state in states.Where(state => state.EmitsSpike))
            {
                foreach (int target in state.Neuron.Connections)
                {
                    states[target - 1].ReceiveSpike();
                }
            }
            StepCount++;
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

        private sealed class NeuronState
        {
            private Rule? selectedRule;
            private int remainingDelay;
            private SpikeRelease pendingRelease = SpikeRelease.None;

            public NeuronState(Neuron neuron)
            {
                Neuron = neuron;
                Spikes = neuron.InitialSpikes;
            }

            public Neuron Neuron { get; }

            public string Spikes { get; private set; }

            // A delayed rule still emits on the step it is selected; only the spike consumption waits.
            public bool EmitsSpike => selectedRule?.Fire == true;

            public void SelectRule(Random random)
            {
                selectedRule = null;
                if (remainingDelay > 0)
                {
                    return;
                }
                List<Rule> matching = Neuron.Rules.Where(rule => rule.Matches(Spikes)).ToList();
                if (matching.Count > 0)
                {
                    selectedRule = matching[random.Next(matching.Count)];
                }
            }

            public SpikeRelease ReleaseSpikes()
            {
                if (remainingDelay > 0)
                {
                    remainingDelay--;
                    return SpikeRelease.None;
                }
                if (pendingRelease != SpikeRelease.None)
                {
                    SpikeRelease delayed = pendingRelease;
                    pendingRelease = SpikeRelease.None;
                    Spikes = "";
                    return delayed;
                }
                if (selectedRule == null)
                {
                    return SpikeRelease.None;
                }
                SpikeRelease release = selectedRule.Fire ? SpikeRelease.Fired : SpikeRelease.Forgot;
                if (selectedRule.Delay > 0)
                {
                    remainingDelay = selectedRule.Delay;
                    pendingRelease = release;
                    return SpikeRelease.None;
                }
                Spikes = "";
                return release;
            }

            public void ReceiveSpike() => Spikes += "a";
        }
    }
}
