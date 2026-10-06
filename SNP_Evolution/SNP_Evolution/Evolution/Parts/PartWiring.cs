using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Parts
{
    // Positions are 1-based network positions, in the part's own neuron order.
    public sealed record PartCopy(ModuleTag Tag, LibraryPart Part, IReadOnlyList<int> Positions)
    {
        public IEnumerable<CopyPort> Ports => Part.Part.Ports().Select(port => new CopyPort(this, port.Port, Positions[port.Position - 1]));

        public int this[string port] => Ports.Single(copyPort => copyPort.Port.Name == port).Position;
    }

    public sealed record CopyPort(PartCopy Copy, Port Port, int Position);

    public sealed record Wire(CopyPort From, CopyPort To);

    // Neurons outside every part copy have no type, so where no port fits one of them stands in, as for a harvested module.
    public static class PartWiring
    {
        public static bool Fits(Port from, Port to) =>
            from.Direction == PortDirection.Out && to.Direction == PortDirection.In && from.Kind == to.Kind && from.Width == to.Width;

        // A copy whose inside was edited matches no version of its part, so it is no longer typed.
        public static IReadOnlyList<PartCopy> Copies(Network network, ModuleLibrary library)
        {
            var copies = new List<PartCopy>();
            foreach ((ModuleTag tag, List<int> positions) in ModuleEdits.Instances(network))
            {
                if (library.Find(tag.Module)?.Versions.LastOrDefault(part => IsCopyOf(network, positions, part)) is LibraryPart part)
                {
                    copies.Add(new PartCopy(tag, part, positions));
                }
            }
            return copies;
        }

        // Synapses within one copy are its inside, not wires.
        public static IReadOnlyList<Wire> Wires(Network network, ModuleLibrary library)
        {
            List<CopyPort> ports = Copies(network, library).SelectMany(copy => copy.Ports).ToList();
            return ports
                .SelectMany(from => ports
                    .Where(to => to.Copy != from.Copy && Fits(from.Port, to.Port) && network.Neurons[from.Position - 1].Connections.Contains(to.Position))
                    .Select(to => new Wire(from, to)))
                .ToList();
        }

        // Out-ports only feed free in-ports, so inserting a copy never splices into a wire that already works; the task's own in-ports send and its out-ports receive.
        internal static void WirePorts(List<Neuron> neurons, IReadOnlyList<PartCopy> copies, PartCopy added, int offset, Random random, IReadOnlyList<PartPort>? boundary = null)
        {
            List<CopyPort> existing = copies.SelectMany(copy => copy.Ports).ToList();
            HashSet<int> typed = copies.SelectMany(copy => copy.Positions).ToHashSet();
            List<int> untyped = Enumerable.Range(1, offset).Where(position => !typed.Contains(position)).ToList();
            List<PartPort> taskPorts = (boundary ?? Array.Empty<PartPort>()).Where(port => port.Position <= offset && !typed.Contains(port.Position)).ToList();
            foreach (CopyPort port in added.Ports)
            {
                if (port.Port.Direction == PortDirection.In)
                {
                    List<int> senders = existing.Where(other => Fits(other.Port, port.Port)).Select(other => other.Position)
                        .Concat(taskPorts.Where(task => task.Port.Direction == PortDirection.In && SameType(task.Port, port.Port)).Select(task => task.Position))
                        .ToList();
                    Connect(neurons, Pick(senders.Count > 0 ? senders : untyped, random), port.Position);
                }
                else if (!neurons[port.Position - 1].IsOutput)
                {
                    List<int> receivers = existing.Where(other => Fits(port.Port, other.Port) && IsFree(neurons, other)).Select(other => other.Position)
                        .Concat(taskPorts.Where(task => task.Port.Direction == PortDirection.Out && SameType(task.Port, port.Port) && !FedByAPart(neurons, typed, task.Position))
                            .Select(task => task.Position))
                        .ToList();
                    Connect(neurons, port.Position, Pick(receivers.Count > 0 ? receivers : untyped.Where(position => !neurons[position - 1].IsInput).ToList(), random));
                }
            }
        }

        private static bool SameType(Port first, Port second) => first.Kind == second.Kind && first.Width == second.Width;

        private static bool FedByAPart(List<Neuron> neurons, HashSet<int> typed, int position) =>
            typed.Any(sender => neurons[sender - 1].Connections.Contains(position));

        // Whether the neurons at the positions are the part's body: its rules, the synapses among them and its initial spikes.
        public static bool IsCopyOf(Network network, IReadOnlyList<int> positions, LibraryPart part)
        {
            IReadOnlyList<Neuron> body = part.Part.Network.Neurons;
            return body.Count == positions.Count
                && body.Select(neuron => neuron.InitialSpikes).SequenceEqual(positions.Select(position => network.Neurons[position - 1].InitialSpikes))
                && BodyInside(part) == ModuleEdits.Inside(network, positions);
        }

        internal static string BodyInside(LibraryPart part)
        {
            Network body = ModuleLibrary.CutOf(part.Part).Body;
            return ModuleEdits.Inside(body, Enumerable.Range(1, body.Neurons.Count).ToList());
        }

        private static bool IsFree(List<Neuron> neurons, CopyPort port) =>
            !neurons[port.Position - 1].IsInput
            && !neurons.Where((_, index) => !port.Copy.Positions.Contains(index + 1)).Any(neuron => neuron.Connections.Contains(port.Position));

        private static int? Pick(IReadOnlyList<int> positions, Random random) => positions.Count == 0 ? null : positions[random.Next(positions.Count)];

        private static void Connect(List<Neuron> neurons, int? from, int? to)
        {
            if (from is int sender && to is int receiver && sender != receiver)
            {
                neurons[sender - 1] = neurons[sender - 1].WithConnections(neurons[sender - 1].Connections.Append(receiver));
            }
        }
    }
}
