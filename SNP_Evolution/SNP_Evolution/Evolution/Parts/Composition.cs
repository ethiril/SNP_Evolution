using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Parts
{
    // A neuron of a composition: Instance 0 is glue, with Neuron its 1-based place among the glue; otherwise Neuron is
    // the 1-based place within that part instance, in the part's own neuron order.
    public sealed record Endpoint(int Instance, int Neuron)
    {
        public bool IsGlue => Instance == 0;

        public static Endpoint GlueAt(int neuron) => new Endpoint(0, neuron);

        public override string ToString() => IsGlue ? $"g{Neuron}" : $"{Instance}.{Neuron}";
    }

    // A copy of a library part: which copy, which module, and which of the module's versions its body is.
    public sealed record PartInstance(int Instance, int Module, int Version);

    // A neuron outside every part, which evolution may change like any neuron of a flat network.
    public sealed record GlueNeuron(IReadOnlyList<Rule> Rules, long InitialSpikes)
    {
        public Neuron ToNeuron() => new Neuron(Rules, InitialSpikes, Array.Empty<int>(), false);
    }

    // A synapse from an out-port to an in-port of the same kind and width on another copy.
    public sealed record PortWire(int FromInstance, string FromPort, int ToInstance, string ToPort)
    {
        public override string ToString() => $"{FromInstance}.{FromPort}>{ToInstance}.{ToPort}";
    }

    // Any other synapse that is not inside one part: to or from glue, or between ports that do not fit.
    public sealed record Link(Endpoint From, Endpoint To)
    {
        public override string ToString() => $"{From}>{To}";
    }

    // A network written as part instances, glue and the synapses between them, laid out glue first so the input glue a task feeds stays ahead of every part.
    public sealed record Composition(
        IReadOnlyList<PartInstance> Parts,
        IReadOnlyList<GlueNeuron> Glue,
        IReadOnlyList<PortWire> Wires,
        IReadOnlyList<Link> Links,
        IReadOnlyList<Endpoint> Inputs,
        IReadOnlyList<Endpoint> Outputs)
    {
        private string? key;

        public string Key => key ??= string.Join(" ",
            "parts " + string.Join(",", Parts.Select(part => $"{part.Instance}:{part.Module}:{part.Version}")),
            "glue " + string.Join(",", Glue.Select(glue => $"{ModuleCuts.RulesText(glue.ToNeuron())}/{glue.InitialSpikes}")),
            "wires " + string.Join(",", Wires.Select(wire => wire.ToString()).Order(StringComparer.Ordinal)),
            "links " + string.Join(",", Links.Select(link => link.ToString()).Order(StringComparer.Ordinal)),
            "inputs " + string.Join(",", Inputs.Select(input => input.ToString()).Order(StringComparer.Ordinal)),
            "outputs " + string.Join(",", Outputs.Select(output => output.ToString()).Order(StringComparer.Ordinal)));

        public bool Equals(Composition? other) => other != null && Key == other.Key;

        public override int GetHashCode() => Key.GetHashCode();

        // The part the instance is a copy of; throws when the library has no such module or version.
        public static LibraryPart PartOf(PartInstance instance, ModuleLibrary library) =>
            library.Find(instance.Module)?.Versions.ElementAtOrDefault(instance.Version)
            ?? throw new ArgumentException($"The library has no version {instance.Version} of module {instance.Module}.");

        // The endpoint at each position of the flattened network, position 1 first.
        public IReadOnlyList<Endpoint> Layout(ModuleLibrary library) =>
            Enumerable.Range(1, Glue.Count).Select(Endpoint.GlueAt)
                .Concat(Parts.SelectMany(part => Enumerable.Range(1, ModuleLibrary.CutOf(PartOf(part, library).Part).Body.Neurons.Count).Select(neuron => new Endpoint(part.Instance, neuron))))
                .ToList();

        public Network Flatten(ModuleLibrary library)
        {
            var bodies = Parts.Select(part => (part.Instance, Part: PartOf(part, library)))
                .ToDictionary(copy => copy.Instance, copy => (copy.Part, Body: ModuleLibrary.CutOf(copy.Part.Part).Body));
            var offsets = new Dictionary<int, int>();
            int next = Glue.Count;
            foreach (PartInstance part in Parts)
            {
                offsets[part.Instance] = next;
                next += bodies[part.Instance].Body.Neurons.Count;
            }
            int Position(Endpoint endpoint) => endpoint.IsGlue ? endpoint.Neuron : offsets[endpoint.Instance] + endpoint.Neuron;
            int PortPosition(int instance, string port) => offsets[instance] + bodies[instance].Part.Part.Ports().Single(each => each.Port.Name == port).Position;

            var neurons = Glue.Select(glue => glue.ToNeuron()).ToList();
            foreach (PartInstance part in Parts)
            {
                int offset = offsets[part.Instance];
                var tag = new ModuleTag(part.Module, part.Instance);
                neurons.AddRange(bodies[part.Instance].Body.Neurons.Select(neuron => neuron
                    .WithConnections(neuron.Connections.Select(target => target + offset))
                    .WithRoles(false, false)
                    .WithModule(tag)));
            }
            var synapses = Wires.Select(wire => (From: PortPosition(wire.FromInstance, wire.FromPort), To: PortPosition(wire.ToInstance, wire.ToPort)))
                .Concat(Links.Select(link => (From: Position(link.From), To: Position(link.To))));
            foreach (IGrouping<int, (int From, int To)> outgoing in synapses.GroupBy(synapse => synapse.From))
            {
                Neuron sender = neurons[outgoing.Key - 1];
                neurons[outgoing.Key - 1] = sender.WithConnections(sender.Connections.Concat(outgoing.Select(synapse => synapse.To)));
            }
            HashSet<int> inputs = Inputs.Select(Position).ToHashSet(), outputs = Outputs.Select(Position).ToHashSet();
            return new Network(neurons.Select((neuron, index) => neuron.WithRoles(outputs.Contains(index + 1), inputs.Contains(index + 1))).ToList());
        }

        // Null when a tagged copy differs from every version of its part in rules, inside synapses or initial spikes.
        public static Composition? Recover(Network network, ModuleLibrary library)
        {
            var endpoints = new Endpoint?[network.Neurons.Count + 1];
            var parts = new List<PartInstance>();
            var copies = new Dictionary<int, LibraryPart>();
            foreach ((ModuleTag tag, List<int> positions) in ModuleEdits.Instances(network).OrderBy(instance => instance.Value[0]))
            {
                if (library.Find(tag.Module) is not Module module || copies.ContainsKey(tag.Instance))
                {
                    return null;
                }
                int version = Enumerable.Range(0, module.Versions.Count).LastOrDefault(index => PartWiring.IsCopyOf(network, positions, module.Versions[index]), -1);
                if (version < 0)
                {
                    return null;
                }
                parts.Add(new PartInstance(tag.Instance, tag.Module, version));
                copies[tag.Instance] = module.Versions[version];
                for (int index = 0; index < positions.Count; index++)
                {
                    endpoints[positions[index]] = new Endpoint(tag.Instance, index + 1);
                }
            }
            var glue = new List<GlueNeuron>();
            for (int position = 1; position <= network.Neurons.Count; position++)
            {
                if (endpoints[position] == null)
                {
                    Neuron neuron = network.Neurons[position - 1];
                    glue.Add(new GlueNeuron(neuron.Rules, neuron.InitialSpikes));
                    endpoints[position] = Endpoint.GlueAt(glue.Count);
                }
            }
            // Every position now has an endpoint; slot 0 is unused, since positions count from 1.
            Endpoint[] placed = endpoints.Select(endpoint => endpoint ?? Endpoint.GlueAt(0)).ToArray();
            (List<PortWire> wires, List<Link> links) = Synapses(network, placed, copies.ToDictionary(copy => copy.Key, copy => PortsByPosition(copy.Value)));
            List<Endpoint> Having(Func<Neuron, bool> role) =>
                Enumerable.Range(1, network.Neurons.Count).Where(position => role(network.Neurons[position - 1])).Select(position => placed[position]).ToList();
            return new Composition(parts, glue, wires, links, Having(neuron => neuron.IsInput), Having(neuron => neuron.IsOutput));
        }

        // Synapses between parts that join fitting ports are wires, ones inside a part are its body, and the rest are links.
        private static (List<PortWire> Wires, List<Link> Links) Synapses(Network network, Endpoint[] endpoints, Dictionary<int, Dictionary<int, Port>> ports)
        {
            var wires = new List<PortWire>();
            var links = new List<Link>();
            for (int position = 1; position <= network.Neurons.Count; position++)
            {
                Endpoint from = endpoints[position];
                foreach (Endpoint to in network.Neurons[position - 1].Connections.Select(target => endpoints[target]))
                {
                    if (!from.IsGlue && from.Instance == to.Instance)
                    {
                        continue;
                    }
                    if (!from.IsGlue && !to.IsGlue
                        && ports[from.Instance].TryGetValue(from.Neuron, out Port? sent) && ports[to.Instance].TryGetValue(to.Neuron, out Port? received)
                        && PartWiring.Fits(sent, received))
                    {
                        wires.Add(new PortWire(from.Instance, sent.Name, to.Instance, received.Name));
                    }
                    else
                    {
                        links.Add(new Link(from, to));
                    }
                }
            }
            return (wires, links);
        }

        internal static Dictionary<int, Port> PortsByPosition(LibraryPart part) => part.Part.Ports().ToDictionary(port => port.Position, port => port.Port);

        // Whether nothing reaches a part's insides except through its ports.
        public bool ThroughPorts(ModuleLibrary library) => Links.All(PassesThroughPorts(library));

        // ModuleEdits.Insert can link the old output to a part port that takes the output role, which this drops.
        public Composition OnlyThroughPorts(ModuleLibrary library) => this with { Links = Links.Where(PassesThroughPorts(library)).ToList() };

        private Func<Link, bool> PassesThroughPorts(ModuleLibrary library)
        {
            Dictionary<int, Dictionary<int, Port>> ports = Parts.ToDictionary(part => part.Instance, part => PortsByPosition(PartOf(part, library)));
            bool Is(Endpoint endpoint, PortDirection direction) =>
                endpoint.IsGlue || (ports[endpoint.Instance].TryGetValue(endpoint.Neuron, out Port? port) && port.Direction == direction);
            return link => Is(link.From, PortDirection.Out) && Is(link.To, PortDirection.In);
        }

        // Without the instance and every synapse to or from it; glue left with no synapses stays.
        public Composition Without(int instance) => this with
        {
            Parts = Parts.Where(part => part.Instance != instance).ToList(),
            Wires = Wires.Where(wire => wire.FromInstance != instance && wire.ToInstance != instance).ToList(),
            Links = Links.Where(link => link.From.Instance != instance && link.To.Instance != instance).ToList(),
            Inputs = Inputs.Where(input => input.Instance != instance).ToList(),
            Outputs = Outputs.Where(output => output.Instance != instance).ToList(),
        };
    }
}
