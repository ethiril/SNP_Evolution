using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Modules
{
    // The edits composition search makes and how often, relative to each other. GlueEdits weighs the ordinary neuron
    // edits as a group, which act on glue only. MaxParts caps the part copies in a network, StartParts is the most a
    // starting network gets, and MaxGlue caps the glue neurons, which is the genome space's neuron cap when 0.
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

    // Search over compositions of library parts. Every network it makes is the flattened form of a composition whose
    // part copies are exact library parts, wired through their ports, with no more glue than the genome space allows
    // neurons; part bodies do not count against that, since the cap bounds the search and they are verified already.
    // Ordinary neuron edits change glue only, and part edits put copies in, take them out, rewire their ports and swap
    // them for cheaper versions. Any algorithm can search this way, since it only sees networks.
    public sealed class CompositionSpace
    {
        private readonly ModuleLibrary library;
        private readonly NetworkFactory glueFactory;
        private readonly NetworkFactory editFactory;
        private readonly CompositionMix mix;
        private readonly Random random;

        public CompositionSpace(ModuleLibrary library, NetworkFactory glueFactory, CompositionMix mix, Random random)
        {
            this.library = library;
            this.glueFactory = glueFactory = mix.MaxGlue > 0 ? glueFactory.WithSpace(glueFactory.Space with { MaxNeurons = mix.MaxGlue }) : glueFactory;
            this.mix = mix;
            this.random = random;
            // Edits that check the network's size see room for every part on top of the glue, and the guard holds glue to the cap.
            int partRoom = mix.MaxParts * Math.Max(1, library.Parts.Select(module => module.Body.Neurons.Count).DefaultIfEmpty(0).Max());
            editFactory = glueFactory.WithSpace(glueFactory.Space with { MaxNeurons = glueFactory.Space.MaxNeurons + partRoom });
        }

        public static CompositionSpace For(EvolutionContext context) =>
            new CompositionSpace(context.Parts ?? new ModuleLibrary(), context.Factory, context.Composition ?? new CompositionMix(), context.Random);

        public ModuleLibrary Library => library;

        public int MaxGlue => glueFactory.Space.MaxNeurons;

        public Network NewNetwork() => Composition.Random(library, glueFactory, random.Next(1, mix.StartParts + 1), random).Flatten(library);

        public WeightedMutation Mutation(float rate, MutationPressure? pressure = null)
        {
            IEnumerable<WeightedEdit> glue = WeightedMutation.Structural(rate, editFactory).Edits
                .Select(edit => edit with { Edit = new CompositionEdit(this, edit.Edit, glueOnly: true), Weight = edit.Weight * mix.GlueEdits });
            var parts = new[]
            {
                new WeightedEdit("Insert part", new CompositionEdit(this, new InsertModule(library, editFactory.Space)), mix.InsertPart),
                new WeightedEdit("Remove part", new CompositionEdit(this, new RemovePart(library)), mix.RemovePart),
                new WeightedEdit("Rewire port", new CompositionEdit(this, new RewirePort(library)), mix.RewirePort),
                new WeightedEdit("Add glue neuron", new CompositionEdit(this, new AddGlueNeuron(library, editFactory)), mix.AddGlue),
                new WeightedEdit("Swap part", new CompositionEdit(this, new SwapPart(library)), mix.SwapPart),
            };
            return new WeightedMutation(rate, glue.Concat(parts).Where(edit => edit.Weight > 0).ToList(), pressure: pressure);
        }

        // The composition the network is, laid out as Flatten lays it out, when it is one this search may make.
        // A part edit's links that miss the ports are dropped, since inserting a copy can make one; any other edit
        // that makes one is refused.
        internal Composition? Admit(Network network, bool dropStrayLinks)
        {
            if (Composition.Recover(network, library) is not Composition composition)
            {
                return null;
            }
            composition = dropStrayLinks ? composition.OnlyThroughPorts(library) : composition;
            return composition.Glue.Count <= MaxGlue && composition.Parts.Count <= mix.MaxParts && composition.ThroughPorts(library) ? composition : null;
        }
    }

    // Parents are kept whole, since a cut through a part would void its verification and the genome has no other
    // natural place to cut.
    public sealed class KeepFirstParent : ICrossover
    {
        public Network Cross(Network firstParent, Network secondParent, Random random) => firstParent;
    }

    // Makes an edit and keeps the result only when it is a composition the search may make, laid out canonically. A
    // glue-only edit must also leave every part copy, its wires and the synapses between parts as they were, and may
    // only drop a port's synapses to glue, never add one. An edit that fails is tried again a few times, and otherwise
    // the network is left as it was.
    internal sealed class CompositionEdit : IMutation
    {
        private const int Attempts = 4;

        private readonly CompositionSpace space;
        private readonly IMutation edit;
        private readonly bool glueOnly;

        public CompositionEdit(CompositionSpace space, IMutation edit, bool glueOnly = false)
        {
            this.space = space;
            this.edit = edit;
            this.glueOnly = glueOnly;
        }

        public Network Mutate(Network network, Random random)
        {
            Composition? before = glueOnly ? Composition.Recover(network, space.Library) : null;
            for (int attempt = 0; attempt < Attempts; attempt++)
            {
                Network changed = edit.Mutate(network, random);
                if (changed != network && space.Admit(changed, !glueOnly) is Composition after && (!glueOnly || (before != null && KeepsParts(before, after))))
                {
                    return after.Flatten(space.Library);
                }
            }
            return network;
        }

        private static bool KeepsParts(Composition before, Composition after)
        {
            if (!before.Parts.SequenceEqual(after.Parts) || !before.Wires.ToHashSet().SetEquals(after.Wires))
            {
                return false;
            }
            static bool BetweenParts(Link link) => !link.From.IsGlue && !link.To.IsGlue;
            static Dictionary<Endpoint, int> ToGlue(Composition composition) =>
                composition.Links.Where(link => !link.From.IsGlue && link.To.IsGlue).GroupBy(link => link.From).ToDictionary(group => group.Key, group => group.Count());
            Dictionary<Endpoint, int> was = ToGlue(before);
            return before.Links.Where(BetweenParts).ToHashSet().SetEquals(after.Links.Where(BetweenParts))
                && ToGlue(after).All(sender => sender.Value <= was.GetValueOrDefault(sender.Key));
        }
    }

    // Takes one part copy out with every synapse to or from it. A copy holding the network's input or output role
    // stays, since taking it out would change what the task can feed or read.
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
