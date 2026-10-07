using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Model;
using SnpEvolution.Search.Operators;
using SnpEvolution.Specs.Parts;

namespace SnpEvolution.Search.Modules
{
    // Edit weights are relative to each other, and MaxGlue 0 means the genome space's neuron cap.
    public sealed record CompositionMix(
        double GlueEdits = 1,
        double InsertPart = 0.75,
        double RemovePart = 0.25,
        double RewirePort = 0.75,
        double AddGlue = 0.25,
        double SwapPart = 0.1,
        int MaxParts = 8,
        int StartParts = 2,
        int MaxGlue = 0);

    // Parents are kept whole, since a cut through a part would void its verification.
    public sealed class KeepFirstParent : ICrossover
    {
        public Network Cross(Network firstParent, Network secondParent, Random random) => firstParent;
    }

    // A copy holding the input or output role stays, since taking it out would change what the task can feed or read.
    public sealed class RemovePart : IMutation
    {
        private readonly ModuleLibrary library;

        public RemovePart(ModuleLibrary library) => this.library = library;

        public Network Mutate(Network network, Random random)
        {
            if (Composition.Recover(network, library) is not Composition composition)
            {
                return network;
            }
            HashSet<int> holdingRoles = composition.Inputs.Concat(composition.Outputs).Select(endpoint => endpoint.Instance).ToHashSet();
            List<PartInstance> removable = composition.Parts.Where(part => !holdingRoles.Contains(part.Instance)).ToList();
            return removable.Count == 0 ? network : composition.Without(removable[random.Next(removable.Count)].Instance).Flatten(library);
        }
    }
}
