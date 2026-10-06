using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Genome;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Modules
{
    // Puts a copy of a module from the library into the network.
    public sealed class InsertModule : IMutation
    {
        private readonly ModuleLibrary library;
        private readonly GenomeSpace space;
        private readonly IReadOnlyList<PartPort>? boundary;

        public InsertModule(ModuleLibrary library, GenomeSpace space, IReadOnlyList<PartPort>? boundary = null)
        {
            this.library = library;
            this.space = space;
            this.boundary = boundary;
        }

        public Network Mutate(Network network, Random random) =>
            library.Choose(random) is Module module ? ModuleEdits.Insert(network, module, library.NextInstance(), space.MaxNeurons, library, random, boundary) : network;
    }

    // Frees one module copy, so its neurons can change like any other from now on.
    public sealed class DissolveModule : IMutation
    {
        public Network Mutate(Network network, Random random)
        {
            List<KeyValuePair<ModuleTag, List<int>>> instances = ModuleEdits.Instances(network).ToList();
            if (instances.Count == 0)
            {
                return network;
            }
            HashSet<int> freed = instances[random.Next(instances.Count)].Value.ToHashSet();
            return new Network(network.Neurons.Select((neuron, index) => freed.Contains(index + 1) ? neuron.WithModule(null) : neuron).ToList());
        }
    }

    // Lets another edit change anything but the inside of a module copy: an edit that would is tried again, a few
    // times, and otherwise the network is left as it was.
    public sealed class ProtectModules : IMutation
    {
        private const int Attempts = 3;

        private readonly IMutation edit;

        public ProtectModules(IMutation edit) => this.edit = edit;

        public Network Mutate(Network network, Random random)
        {
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                Network changed = edit.Mutate(network, random);
                if (ModuleEdits.KeepsModules(network, changed))
                {
                    return changed;
                }
            }
            return network;
        }
    }
}
