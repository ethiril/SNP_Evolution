using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Model;
using SnpEvolution.Search.Genome;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;

namespace SnpEvolution.Search
{
    // A random composition for a search to start from: random glue and library parts wired in by port type.
    public static class RandomComposition
    {
        // Glue holds a relay per input, an output and up to two more neurons, and parts are wired in by port type.
        public static Composition Of(ModuleLibrary library, NetworkFactory glueFactory, int parts, Random random, IReadOnlyList<PartPort>? boundary = null)
        {
            GenomeSpace space = glueFactory.Space;
            Network network = boundary != null
                ? QuietGlue(glueFactory, boundary)
                : glueFactory.WithSpace(space with { MaxNeurons = Math.Min(space.MaxNeurons, space.InputCount + 3) }).NewNetwork();
            network = new Network(network.Neurons.Select(neuron => neuron.IsInput ? neuron.WithRules(new[] { glueFactory.RelayRule() }) : neuron).ToList());
            IReadOnlyList<Module> available = library.Parts;
            for (int part = 0; part < parts && available.Count > 0; part++)
            {
                network = ModuleEdits.Insert(network, available[random.Next(available.Count)], library.NextInstance(), int.MaxValue, library, random, boundary);
            }
            return (Composition.Recover(network, library) ?? throw new InvalidOperationException("A freshly built composition could not be read back.")).OnlyThroughPorts(library);
        }

        // With a contract the parts and the task's ports give the structure, so random glue would only add noise.
        private static Network QuietGlue(NetworkFactory glueFactory, IReadOnlyList<PartPort> boundary)
        {
            int count = Math.Max(glueFactory.Space.InputCount, boundary.Select(port => port.Position).DefaultIfEmpty(0).Max()) + 1;
            return new Network(Enumerable.Range(1, count)
                .Select(position => new Neuron(new[] { glueFactory.RelayRule() }, 0, Array.Empty<int>(), false, isInput: position <= glueFactory.Space.InputCount))
                .ToList());
        }
    }
}
