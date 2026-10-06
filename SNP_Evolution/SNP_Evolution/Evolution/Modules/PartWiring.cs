using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Modules
{
    // A copy of a verified part in a network: the version of the part its neurons still match, and their 1-based
    // positions in the network, in the part's own order.
    public sealed record PartCopy(ModuleTag Tag, LibraryPart Part, IReadOnlyList<int> Positions)
    {
        public IEnumerable<CopyPort> Ports => Part.Part.Ports().Select(port => new CopyPort(this, port.Port, Positions[port.Position - 1]));

        // The network position of the named port's neuron.
        public int this[string port] => Ports.Single(copyPort => copyPort.Port.Name == port).Position;
    }

    public sealed record CopyPort(PartCopy Copy, Port Port, int Position);

    // A synapse from an out-port of one part copy to a fitting in-port of another.
    public sealed record Wire(CopyPort From, CopyPort To);

    // Wiring part copies to each other by port type. Neurons outside every part copy have no type, so where no port fits,
    // one of them stands in, as for a module without a contract.
    public static class PartWiring
    {
        // A done to a start, a count out to a count in: out to in, of the same kind and binary width. Any other wire is
        // never right, so no edit makes one.
        public static bool Fits(Port from, Port to) =>
            from.Direction == PortDirection.Out && to.Direction == PortDirection.In && from.Kind == to.Kind && from.Width == to.Width;

        // Every module copy whose neurons still match a version of a library part; a copy whose inside was edited no longer
        // does, so it is no longer typed.
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

        public static IReadOnlyList<Wire> Wires(Network network, ModuleLibrary library) => Wires(network, Copies(network, library));

        // The part's own network with every neuron tagged as one copy of the module, so a network can start from a part,
        // its in-ports being the network's inputs.
        public static Network AsCopy(Module module, int instance)
        {
            var tag = new ModuleTag(module.Id, instance);
            return new Network(module.Part!.Part.Network.Neurons.Select(neuron => neuron.WithModule(tag)).ToList());
        }

        // Wires the ports of a copy just added after the first offset neurons. Each in-port is fed by a fitting out-port of
        // a copy already there, and each out-port feeds a fitting in-port that is not an input and that nothing outside
        // its own copy feeds yet. A port with nothing fitting takes a random untyped neuron instead, which for an out-port
        // is not an input; a port that took the network's output sends nowhere, as a module's would.
        internal static void WirePorts(List<Neuron> neurons, IReadOnlyList<PartCopy> copies, PartCopy added, int offset, Random random)
        {
            List<CopyPort> existing = copies.SelectMany(copy => copy.Ports).ToList();
            HashSet<int> typed = copies.SelectMany(copy => copy.Positions).ToHashSet();
            List<int> untyped = Enumerable.Range(1, offset).Where(position => !typed.Contains(position)).ToList();
            foreach (CopyPort port in added.Ports)
            {
                if (port.Port.Direction == PortDirection.In)
                {
                    List<int> senders = existing.Where(other => Fits(other.Port, port.Port)).Select(other => other.Position).ToList();
                    Connect(neurons, Pick(senders.Count > 0 ? senders : untyped, random), port.Position);
                }
                else if (!neurons[port.Position - 1].IsOutput)
                {
                    List<int> receivers = existing.Where(other => Fits(port.Port, other.Port) && IsFree(neurons, other)).Select(other => other.Position).ToList();
                    Connect(neurons, port.Position, Pick(receivers.Count > 0 ? receivers : untyped.Where(position => !neurons[position - 1].IsInput).ToList(), random));
                }
            }
        }

        // The copy rebuilt from the replacement module's part, which has the same contract, in the copy's place: wires to
        // and from each port move to the port of the same name, and so do the input and output roles. Unchanged when
        // the network would grow past maxNeurons or a role sits on a neuron that is no port.
        public static Network Swap(Network network, PartCopy copy, Module replacement, int maxNeurons)
        {
            Network body = replacement.Body;
            if (network.Neurons.Count - copy.Positions.Count + body.Neurons.Count > maxNeurons)
            {
                return network;
            }
            var old = copy.Positions.ToHashSet();
            Dictionary<int, string> oldPorts = copy.Ports.ToDictionary(port => port.Position, port => port.Port.Name);
            if (copy.Positions.Any(position => !oldPorts.ContainsKey(position) && !NetworkEdits.IsHidden(network.Neurons[position - 1])))
            {
                return network;
            }
            Dictionary<string, int> newPorts = replacement.Part!.Part.Ports().ToDictionary(port => port.Port.Name, port => port.Position);
            int first = copy.Positions.Min();
            var moved = new Dictionary<int, int>();
            int next = 1;
            for (int position = 1; position <= network.Neurons.Count; position++)
            {
                next += position == first ? body.Neurons.Count : 0;
                if (!old.Contains(position))
                {
                    moved[position] = next++;
                }
            }
            foreach ((int position, string name) in oldPorts)
            {
                moved[position] = first - 1 + newPorts[name];
            }
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

        internal static IReadOnlyList<Wire> Wires(Network network, IReadOnlyList<PartCopy> copies)
        {
            List<CopyPort> ports = copies.SelectMany(copy => copy.Ports).ToList();
            return ports
                .SelectMany(from => ports
                    .Where(to => to.Copy != from.Copy && Fits(from.Port, to.Port) && network.Neurons[from.Position - 1].Connections.Contains(to.Position))
                    .Select(to => new Wire(from, to)))
                .ToList();
        }

        private static string BodyInside(LibraryPart part)
        {
            Network body = ModuleLibrary.CutOf(part.Part).Body;
            return ModuleEdits.Inside(body, Enumerable.Range(1, body.Neurons.Count).ToList());
        }

        // Fed by nothing outside its own copy and not one of the network's inputs.
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

    // Moves one end of a part copy's port: an in-port that is not an input drops what fed it from outside its copy and
    // is fed by a fitting out-port of another copy instead, or an out-port stops sending outside its copy and sends to
    // a fitting in-port of another copy. Unchanged when no port has a fitting partner.
    public sealed class RewirePort : IMutation
    {
        private readonly ModuleLibrary library;

        public RewirePort(ModuleLibrary library) => this.library = library;

        public Network Mutate(Network network, Random random)
        {
            List<CopyPort> ports = PartWiring.Copies(network, library).SelectMany(copy => copy.Ports).ToList();
            var choices = ports
                .Where(port => !network.Neurons[port.Position - 1].IsInput)
                .Select(port => (Port: port, Partners: ports.Where(other => other.Copy != port.Copy && (port.Port.Direction == PortDirection.In
                    ? PartWiring.Fits(other.Port, port.Port)
                    : PartWiring.Fits(port.Port, other.Port) && !network.Neurons[other.Position - 1].IsInput)).ToList()))
                .Where(choice => choice.Partners.Count > 0)
                .ToList();
            if (choices.Count == 0)
            {
                return network;
            }
            (CopyPort chosen, List<CopyPort> partners) = choices[random.Next(choices.Count)];
            int partner = partners[random.Next(partners.Count)].Position;
            HashSet<int> own = chosen.Copy.Positions.ToHashSet();
            if (chosen.Port.Direction == PortDirection.Out)
            {
                IEnumerable<int> inside = network.Neurons[chosen.Position - 1].Connections.Where(own.Contains);
                return NetworkEdits.SetConnections(network, chosen.Position - 1, inside.Append(partner));
            }
            return new Network(network.Neurons.Select((neuron, index) =>
                own.Contains(index + 1) ? neuron
                : index + 1 == partner ? neuron.WithConnections(neuron.Connections.Append(chosen.Position))
                : neuron.WithConnections(neuron.Connections.Where(target => target != chosen.Position))).ToList());
        }
    }

    // Puts a relay neuron, which evolution can then change like any other, on a wire between two part copies.
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

    // Replaces a part copy with the cheapest part in the library that has the same contract, when that is cheaper than
    // the copy, including a cheaper version that has since replaced the copy's own part under the same module.
    public sealed class SwapPart : IMutation
    {
        private readonly ModuleLibrary library;
        private readonly int maxNeurons;

        public SwapPart(ModuleLibrary library, int maxNeurons)
        {
            this.library = library;
            this.maxNeurons = maxNeurons;
        }

        public Network Mutate(Network network, Random random)
        {
            IReadOnlyList<Module> parts = library.Parts;
            var swaps = PartWiring.Copies(network, library)
                .Select(copy => (Copy: copy, Cheapest: parts
                    .Where(module => module.Part!.Contract.ToJson() == copy.Part.Contract.ToJson() && module.Part.Cost.CompareTo(copy.Part.Cost) < 0)
                    .OrderBy(module => module.Part!.Cost, HardwareCost.SmallestFirst)
                    .FirstOrDefault()))
                .Where(swap => swap.Cheapest != null)
                .ToList();
            if (swaps.Count == 0)
            {
                return network;
            }
            (PartCopy copy, Module? cheapest) = swaps[random.Next(swaps.Count)];
            return PartWiring.Swap(network, copy, cheapest!, maxNeurons);
        }
    }
}
