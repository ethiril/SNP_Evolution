using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Parts
{
    public sealed record RecipePart(int Instance, string Contract);

    public sealed record RecipeLink(string From, string To);

    // Endpoints name ports ("2.sum") rather than positions ("g3" is glue), so a cheaper part that later replaces a child is still wired the same way.
    public sealed record PartRecipe(
        IReadOnlyList<RecipePart> Parts,
        IReadOnlyList<GlueNeuron> Glue,
        IReadOnlyList<PortWire> Wires,
        IReadOnlyList<RecipeLink> Links,
        IReadOnlyList<string> Inputs,
        IReadOnlyList<string> Outputs,
        IReadOnlyDictionary<string, string> Binding)
    {
        // Throws ArgumentException when the library has no part for a child's contract.
        public (Composition Composition, PortBinding Binding) Build(ModuleLibrary library)
        {
            var instances = new List<PartInstance>();
            foreach (RecipePart part in Parts)
            {
                Module module = library.PartFor(part.Contract)
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

        [JsonIgnore]
        public IEnumerable<string> Children => Parts.Select(part => part.Contract).Distinct();
    }
}
