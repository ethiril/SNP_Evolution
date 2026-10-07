using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Model;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;

namespace SnpEvolution.Export
{
    // A port of an exported network: the environment's spikes into an input neuron, or what a neuron sends.
    public sealed record NetworkPort(string Name, int Neuron, bool IsInput)
    {
        public static IReadOnlyList<NetworkPort> ForPart(Part part) =>
            part.Ports().Select(port => new NetworkPort(port.Port.Name, port.Position, port.Port.Direction == PortDirection.In)).ToList();

        // A network with no contract has inputs in1, in2, ... and outputs out (out1, out2, ... for several output neurons).
        public static IReadOnlyList<NetworkPort> Plain(Network network)
        {
            List<int> inputs = network.Neurons.Select((neuron, index) => (neuron, index)).Where(pair => pair.neuron.IsInput).Select(pair => pair.index + 1).ToList();
            List<int> outputs = network.Neurons.Select((neuron, index) => (neuron, index)).Where(pair => pair.neuron.IsOutput).Select(pair => pair.index + 1).ToList();
            return inputs.Select((neuron, index) => new NetworkPort($"in{index + 1}", neuron, true))
                .Concat(outputs.Select((neuron, index) => new NetworkPort(outputs.Count == 1 ? "out" : $"out{index + 1}", neuron, false)))
                .ToList();
        }
    }
}
