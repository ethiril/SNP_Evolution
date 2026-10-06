using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Modules
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
                string inside = ModuleEdits.Inside(network, positions);
                if (library.Find(tag.Module)?.Versions.LastOrDefault(part => BodyInside(part) == inside) is LibraryPart part)
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

        // Out-ports only feed free in-ports, so inserting a copy never splices into a wire that already works. The boundary
        // is the task's own ports, where it has a contract: its in-ports send like out-ports of a part, and its out-ports
        // receive like in-ports, so a part can be wired straight to what the task feeds and reads.
        internal static void WirePorts(List<Neuron> neurons, IReadOnlyList<PartCopy> copies, PartCopy added, int offset, Random random, IReadOnlyList<PartPort>? boundary = null)
        {
            List<CopyPort> existing = copies.SelectMany(copy => copy.Ports).ToList();
            HashSet<int> typed = copies.SelectMany(copy => copy.Positions).ToHashSet();
            List<int> untyped = Enumerable.Range(1, offset).Where(position => !typed.Contains(position)).ToList();
            List<PartPort> edge = (boundary ?? Array.Empty<PartPort>()).Where(port => port.Position <= offset && !typed.Contains(port.Position)).ToList();
            foreach (CopyPort port in added.Ports)
            {
                if (port.Port.Direction == PortDirection.In)
                {
                    List<int> senders = existing.Where(other => Fits(other.Port, port.Port)).Select(other => other.Position)
                        .Concat(edge.Where(task => task.Port.Direction == PortDirection.In && SameType(task.Port, port.Port)).Select(task => task.Position))
                        .ToList();
                    Connect(neurons, Pick(senders.Count > 0 ? senders : untyped, random), port.Position);
                }
                else if (!neurons[port.Position - 1].IsOutput)
                {
                    List<int> receivers = existing.Where(other => Fits(port.Port, other.Port) && IsFree(neurons, other)).Select(other => other.Position)
                        .Concat(edge.Where(task => task.Port.Direction == PortDirection.Out && SameType(task.Port, port.Port) && !FedByAPart(neurons, typed, task.Position))
                            .Select(task => task.Position))
                        .ToList();
                    Connect(neurons, port.Position, Pick(receivers.Count > 0 ? receivers : untyped.Where(position => !neurons[position - 1].IsInput).ToList(), random));
                }
            }
        }

        private static bool SameType(Port first, Port second) => first.Kind == second.Kind && first.Width == second.Width;

        private static bool FedByAPart(List<Neuron> neurons, HashSet<int> typed, int position) =>
            typed.Any(sender => neurons[sender - 1].Connections.Contains(position));

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

    // Moves one end of a port to a fitting port of another copy, or to a neuron outside every copy, which has no type and
    // so fits any port, as it does when a part is put in; input neurons stay as they are, since only the environment feeds them.
    public sealed class RewirePort : IMutation
    {
        private readonly ModuleLibrary library;

        public RewirePort(ModuleLibrary library) => this.library = library;

        public Network Mutate(Network network, Random random)
        {
            IReadOnlyList<PartCopy> copies = PartWiring.Copies(network, library);
            List<CopyPort> ports = copies.SelectMany(copy => copy.Ports).ToList();
            HashSet<int> typed = copies.SelectMany(copy => copy.Positions).ToHashSet();
            List<int> untyped = Enumerable.Range(1, network.Neurons.Count).Where(position => !typed.Contains(position)).ToList();
            var choices = ports
                .Where(port => !network.Neurons[port.Position - 1].IsInput)
                .Select(port => (Port: port, Partners: Partners(network, ports, untyped, port)))
                .Where(choice => choice.Partners.Count > 0)
                .ToList();
            if (choices.Count == 0)
            {
                return network;
            }
            (CopyPort chosen, List<int> partners) = choices[random.Next(choices.Count)];
            int partner = partners[random.Next(partners.Count)];
            return chosen.Port.Direction == PortDirection.Out ? SendTo(network, chosen, partner) : FeedFrom(network, chosen, partner);
        }

        private static List<int> Partners(Network network, List<CopyPort> ports, List<int> untyped, CopyPort port) => ports
            .Where(other => other.Copy != port.Copy)
            .Where(other => port.Port.Direction == PortDirection.In
                ? PartWiring.Fits(other.Port, port.Port)
                : PartWiring.Fits(port.Port, other.Port) && !network.Neurons[other.Position - 1].IsInput)
            .Select(other => other.Position)
            .Concat(untyped.Where(position => port.Port.Direction == PortDirection.In || !network.Neurons[position - 1].IsInput))
            .ToList();

        private static Network SendTo(Network network, CopyPort outPort, int receiver)
        {
            IEnumerable<int> inside = network.Neurons[outPort.Position - 1].Connections.Where(outPort.Copy.Positions.Contains);
            return NetworkEdits.SetConnections(network, outPort.Position - 1, inside.Append(receiver));
        }

        private static Network FeedFrom(Network network, CopyPort inPort, int sender)
        {
            HashSet<int> own = inPort.Copy.Positions.ToHashSet();
            return new Network(network.Neurons.Select((neuron, index) =>
                own.Contains(index + 1) ? neuron
                : index + 1 == sender ? neuron.WithConnections(neuron.Connections.Append(inPort.Position))
                : neuron.WithConnections(neuron.Connections.Where(target => target != inPort.Position))).ToList());
        }
    }

    // The relay is untyped, so evolution can change it like any other neuron.
    public sealed class AddGlueNeuron : IMutation
    {
        private readonly ModuleLibrary library;
        private readonly NetworkFactory factory;

        public AddGlueNeuron(ModuleLibrary library, NetworkFactory factory)
        {
            this.library = library;
            this.factory = factory;
        }

        public Network Mutate(Network network, Random random)
        {
            IReadOnlyList<Wire> wires = PartWiring.Wires(network, library);
            if (wires.Count == 0 || network.Neurons.Count >= factory.Space.MaxNeurons)
            {
                return network;
            }
            Wire wire = wires[random.Next(wires.Count)];
            int from = wire.From.Position - 1;
            Network rewired = NetworkEdits.SetConnections(network, from, network.Neurons[from].Connections.Where(target => target != wire.To.Position).Append(network.Neurons.Count + 1));
            return NetworkEdits.AddNeuron(rewired, new Neuron(new[] { factory.RelayRule() }, 0, new[] { wire.To.Position }, false));
        }
    }

    // Wires and the input and output roles move to the port of the same name, so the swap keeps what the copy was doing.
    public sealed class SwapPart : IMutation
    {
        private readonly ModuleLibrary library;

        public SwapPart(ModuleLibrary library) => this.library = library;

        public Network Mutate(Network network, Random random)
        {
            var swaps = PartWiring.Copies(network, library)
                .SelectMany(copy => CheapestWithSameContract(copy) is Module cheapest ? new[] { (Copy: copy, Replacement: cheapest) } : [])
                .ToList();
            if (swaps.Count == 0)
            {
                return network;
            }
            (PartCopy copy, Module replacement) = swaps[random.Next(swaps.Count)];
            return HasRoleOffItsPorts(network, copy) ? network : Swap(network, copy, replacement);
        }

        // Library parts always carry a part, and a cheaper version under the copy's own module counts too.
        private Module? CheapestWithSameContract(PartCopy copy) => library.Parts
            .Where(module => module.Part!.Contract.ToJson() == copy.Part.Contract.ToJson() && module.Part.Cost.CompareTo(copy.Part.Cost) < 0)
            .OrderBy(module => module.Part!.Cost, HardwareCost.SmallestFirst)
            .FirstOrDefault();

        // The new body goes where the copy started, so input neurons keep their order; costs rank neurons first, so it never grows the network.
        private static Network Swap(Network network, PartCopy copy, Module replacement)
        {
            Network body = replacement.Body;
            HashSet<int> old = copy.Positions.ToHashSet();
            Dictionary<string, int> newPorts = replacement.Part!.Part.Ports().ToDictionary(port => port.Port.Name, port => port.Position);
            int first = copy.Positions.Min();
            Dictionary<int, int> moved = MovedPositions(network, copy, newPorts, first, body.Neurons.Count);
            var tag = new ModuleTag(replacement.Id, copy.Tag.Instance);
            Dictionary<int, Neuron> wasAt = newPorts.ToDictionary(pair => pair.Value, pair => network.Neurons[copy[pair.Key] - 1]);
            List<Neuron> neurons = network.Neurons
                .Where((_, index) => !old.Contains(index + 1))
                .Select(neuron => neuron.WithConnections(neuron.Connections.Where(moved.ContainsKey).Select(target => moved[target])))
                .ToList();
            neurons.InsertRange(first - 1, body.Neurons.Select((neuron, index) =>
            {
                Neuron? was = wasAt.GetValueOrDefault(index + 1);
                IEnumerable<int> outside = was?.Connections.Where(target => !old.Contains(target)).Select(target => moved[target]) ?? Enumerable.Empty<int>();
                return neuron
                    .WithConnections(neuron.Connections.Select(target => target + first - 1).Concat(outside))
                    .WithRoles(was?.IsOutput ?? false, was?.IsInput ?? false)
                    .WithModule(tag);
            }));
            return new Network(neurons);
        }

        private static bool HasRoleOffItsPorts(Network network, PartCopy copy)
        {
            HashSet<int> ports = copy.Ports.Select(port => port.Position).ToHashSet();
            return copy.Positions.Any(position => !ports.Contains(position) && !NetworkEdits.IsHidden(network.Neurons[position - 1]));
        }

        // Where each old position lands: neurons outside the copy shift around the new body, and the copy's ports go to the new ports of the same name.
        private static Dictionary<int, int> MovedPositions(Network network, PartCopy copy, Dictionary<string, int> newPorts, int first, int bodySize)
        {
            HashSet<int> old = copy.Positions.ToHashSet();
            var moved = new Dictionary<int, int>();
            int next = 1;
            for (int position = 1; position <= network.Neurons.Count; position++)
            {
                next += position == first ? bodySize : 0;
                if (!old.Contains(position))
                {
                    moved[position] = next++;
                }
            }
            foreach (CopyPort port in copy.Ports)
            {
                moved[port.Position] = first - 1 + newPorts[port.Port.Name];
            }
            return moved;
        }
    }
}
