using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Tasks;
using Newtonsoft.Json;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Modules
{
    // A child of a promoted part: which copy, and the contract of the library part it is a copy of.
    public sealed record RecipePart(int Instance, string Contract);

    public sealed record RecipeLink(string From, string To);

    // A promoted part written as references to its children rather than as the network they flatten to, so parts can be
    // built from parts. Endpoints are "g3" for the third glue neuron or "2.sum" for port sum of copy 2: ports by name, so
    // a cheaper part that later takes a child's place is still wired the same way. Binding names the endpoint of each
    // out-port and done port of the contract.
    public sealed record PartRecipe(
        IReadOnlyList<RecipePart> Parts,
        IReadOnlyList<GlueNeuron> Glue,
        IReadOnlyList<PortWire> Wires,
        IReadOnlyList<RecipeLink> Links,
        IReadOnlyList<string> Inputs,
        IReadOnlyList<string> Outputs,
        IReadOnlyDictionary<string, string> Binding)
    {
        // The composition, with each child the newest version of the library part whose contract it names, and the
        // binding as positions in the network it flattens to. Throws ArgumentException when a child is missing.
        public (Composition Composition, PortBinding Binding) Build(ModuleLibrary library)
        {
            var instances = new List<PartInstance>();
            foreach (RecipePart part in Parts)
            {
                Module module = library.Parts.FirstOrDefault(each => each.Part!.Contract.Name == part.Contract)
                    ?? throw new ArgumentException($"The library has no part for contract '{part.Contract}'.");
                instances.Add(new PartInstance(part.Instance, module.Id, module.Versions.Count - 1));
            }
            var shell = new Composition(instances, Glue, Wires, Array.Empty<Link>(), Array.Empty<Endpoint>(), Array.Empty<Endpoint>());
            Dictionary<int, Dictionary<string, int>> ports = instances.ToDictionary(
                instance => instance.Instance,
                instance => Composition.PartOf(instance, library).Part.Ports().ToDictionary(port => port.Port.Name, port => port.Position));
            Endpoint Resolve(string text)
            {
                if (text.StartsWith('g') && int.TryParse(text[1..], out int glue) && glue >= 1 && glue <= Glue.Count)
                {
                    return Endpoint.GlueAt(glue);
                }
                int dot = text.IndexOf('.');
                if (dot > 0 && int.TryParse(text[..dot], out int copy) && ports.TryGetValue(copy, out Dictionary<string, int>? named) && named.TryGetValue(text[(dot + 1)..], out int neuron))
                {
                    return new Endpoint(copy, neuron);
                }
                throw new ArgumentException($"'{text}' names no glue neuron or part port of the recipe.");
            }
            Composition composition = shell with
            {
                Links = Links.Select(link => new Link(Resolve(link.From), Resolve(link.To))).ToList(),
                Inputs = Inputs.Select(Resolve).ToList(),
                Outputs = Outputs.Select(Resolve).ToList(),
            };
            List<Endpoint> layout = composition.Layout(library).ToList();
            var binding = new PortBinding(Binding.ToDictionary(pair => pair.Key, pair => layout.IndexOf(Resolve(pair.Value)) + 1));
            return (composition, binding);
        }

        // Null when an input, output, link end or bound port is a neuron inside a part that is not one of its ports.
        public static PartRecipe? Of(Composition composition, PortBinding binding, ModuleLibrary library)
        {
            Dictionary<int, Dictionary<int, Port>> ports = composition.Parts.ToDictionary(part => part.Instance, part => Composition.PortsByPosition(Composition.PartOf(part, library)));
            string? Name(Endpoint endpoint) =>
                endpoint.IsGlue ? $"g{endpoint.Neuron}"
                : ports[endpoint.Instance].TryGetValue(endpoint.Neuron, out Port? port) ? $"{endpoint.Instance}.{port.Name}"
                : null;
            IReadOnlyList<Endpoint> layout = composition.Layout(library);
            List<string?> inputs = composition.Inputs.Select(Name).ToList(), outputs = composition.Outputs.Select(Name).ToList();
            List<(string? From, string? To)> links = composition.Links.Select(link => (Name(link.From), Name(link.To))).ToList();
            Dictionary<string, string?> bound = binding.Positions.ToDictionary(pair => pair.Key, pair => pair.Value <= layout.Count ? Name(layout[pair.Value - 1]) : null);
            if (inputs.Concat(outputs).Concat(links.SelectMany(link => new[] { link.From, link.To })).Concat(bound.Values).Any(name => name == null))
            {
                return null;
            }
            return new PartRecipe(
                composition.Parts.Select(part => new RecipePart(part.Instance, Composition.PartOf(part, library).Contract.Name)).ToList(),
                composition.Glue,
                composition.Wires,
                links.Select(link => new RecipeLink(link.From!, link.To!)).ToList(),
                inputs!,
                outputs!,
                bound.ToDictionary(pair => pair.Key, pair => pair.Value!));
        }

        // Every contract the recipe names, children first, so a library can be loaded in an order that has them.
        [JsonIgnore]
        public IEnumerable<string> Children => Parts.Select(part => part.Contract).Distinct();
    }

    // A solved composition becomes a part whose contract is the target's. The composition was scored on the target task,
    // which may test less than the contract, so the part is verified on the contract on the exhaustive engine before it
    // is kept. Its size is its children's, each verified already, so the leaf cap on module size does not apply.
    public static class Promotion
    {
        // Null, and logged, when the network is not a composition of the library's parts or the part fails its contract.
        public static Module? PromoteSolved(Network network, ContractTask task, ModuleLibrary library, PartOrigin origin, Action<string> log)
        {
            if (Composition.Recover(network, library) is not Composition composition)
            {
                log($"Not promoted: the network that solved {task.Name} is not a composition of library parts.");
                return null;
            }
            return Promote(composition, task.Contract, task.Binding, library, origin, log);
        }

        public static Module? Promote(Composition composition, Contract contract, PortBinding binding, ModuleLibrary library, PartOrigin origin, Action<string> log)
        {
            if (!composition.ThroughPorts(library) || PartRecipe.Of(composition, binding, library) is not PartRecipe recipe)
            {
                log($"Not promoted to a part for {contract.Name}: something reaches inside a part other than through its ports.");
                return null;
            }
            var part = new Part(contract, composition.Flatten(library), binding);
            PartMeasurement measurement;
            try
            {
                measurement = PartEvolution.Measure(part);
            }
            catch (ArgumentException exception)
            {
                log($"Not promoted to a part for {contract.Name}: {exception.Message}");
                return null;
            }
            if (!measurement.MeetsContract)
            {
                log($"Not promoted to a part for {contract.Name}: it fails the contract ({measurement.Description.Replace(Environment.NewLine, "; ")}).");
                return null;
            }
            Module module = library.AddPart(LibraryPart.Of(part, measurement, origin) with { Recipe = recipe }, origin.Run);
            string children = string.Join(", ", recipe.Parts.Select(child => child.Contract));
            log($"Promoted the composition for {contract.Name} to module {module.Id}: {measurement.Cost}, latency {measurement.Latency}, built from {children}.");
            return module;
        }
    }
}
