using System;
using System.Collections.Generic;
using SnpEvolution.Model;

namespace SnpEvolution.Search.Operators
{
    // The child has the first parent's neurons and roles, and takes each neuron's rules, spikes and synapses from
    // either parent at the same position, so parents with different topologies can still be crossed.
    public sealed class NeuronCrossover : ICrossover
    {
        public Network Cross(Network firstParent, Network secondParent, Random random)
        {
            int count = firstParent.Neurons.Count;
            var neurons = new List<Neuron>();
            for (int index = 0; index < count; index++)
            {
                Neuron own = firstParent.Neurons[index];
                if (index >= secondParent.Neurons.Count || random.NextDouble() < 0.5)
                {
                    neurons.Add(own);
                    continue;
                }
                Neuron donor = secondParent.Neurons[index];
                var connections = new List<int>(NetworkEdits.ValidConnections(donor.Connections, index, count));
                bool usable = connections.Count > 0 || own.IsOutput;
                neurons.Add(usable
                    ? new Neuron(donor.Rules, own.IsInput ? 0 : donor.InitialSpikes, connections, own.IsOutput, own.IsInput)
                    : own);
            }
            return new Network(neurons);
        }
    }
}
