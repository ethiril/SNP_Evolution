using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Modules
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

    // Part bodies are left out of the glue cap, since they are verified already and the cap is there to bound the search.
    public sealed class CompositionSpace
    {
        private readonly ModuleLibrary library;
        private readonly NetworkFactory glueFactory;
        private readonly NetworkFactory editFactory;
        private readonly CompositionMix mix;
        private readonly Random random;
        private readonly IReadOnlyList<PartPort>? boundary;

        public CompositionSpace(ModuleLibrary library, NetworkFactory glueFactory, CompositionMix mix, Random random, IReadOnlyList<PartPort>? boundary = null)
        {
            this.library = library;
            this.boundary = boundary;
            this.glueFactory = glueFactory = mix.MaxGlue > 0 ? glueFactory.WithSpace(glueFactory.Space with { MaxNeurons = mix.MaxGlue }) : glueFactory;
            this.mix = mix;
            this.random = random;
            // Edits that check the network's size see room for every part on top of the glue, and the guard holds glue to the cap.
            // Parts proposed during the run are leaves, so room for the largest leaf is kept even before one exists.
            int partRoom = mix.MaxParts * Math.Max(ModuleLibrary.MaxModuleNeurons, library.Parts.Select(module => module.Body.Neurons.Count).DefaultIfEmpty(0).Max());
            editFactory = glueFactory.WithSpace(glueFactory.Space with { MaxNeurons = glueFactory.Space.MaxNeurons + partRoom });
        }

        public static CompositionSpace For(EvolutionContext context) =>
            new CompositionSpace(context.Parts ?? new ModuleLibrary(), context.Factory, context.Composition ?? new CompositionMix(), context.Random,
                ((context.Evaluator as ITaskEvaluator)?.Task as ContractTask)?.Boundary);

        public ModuleLibrary Library => library;

        public int MaxGlue => glueFactory.Space.MaxNeurons;

        public Network NewNetwork() => Composition.Random(library, glueFactory, random.Next(1, mix.StartParts + 1), random, boundary).Flatten(library);

        public WeightedMutation Mutation(float rate, MutationPressure? pressure = null)
        {
            IEnumerable<WeightedEdit> glue = WeightedMutation.Structural(rate, editFactory).Edits
                .Select(edit => edit with { Edit = new CompositionEdit(this, edit.Edit, glueOnly: true), Weight = edit.Weight * mix.GlueEdits });
            var parts = new[]
            {
                new WeightedEdit("Insert part", new CompositionEdit(this, new InsertModule(library, editFactory.Space, boundary)), mix.InsertPart),
                new WeightedEdit("Remove part", new CompositionEdit(this, new RemovePart(library)), mix.RemovePart),
                new WeightedEdit("Rewire port", new CompositionEdit(this, new RewirePort(library)), mix.RewirePort),
                new WeightedEdit("Add glue neuron", new CompositionEdit(this, new AddGlueNeuron(library, editFactory)), mix.AddGlue),
                new WeightedEdit("Swap part", new CompositionEdit(this, new SwapPart(library)), mix.SwapPart),
            };
            return new WeightedMutation(rate, glue.Concat(parts).Where(edit => edit.Weight > 0).ToList(), pressure: pressure);
        }

        // The network with a copy of the module wired in by port type, or null when the result is not a composition this search may make.
        public Network? WithPart(Network network, Module module) =>
            Admit(ModuleEdits.Insert(network, module, library.NextInstance(), editFactory.Space.MaxNeurons, library, random, boundary), dropStrayLinks: true)?.Flatten(library);

        // Null when the network is not a composition this search may make, after dropping links that miss the ports if asked.
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

    // Parents are kept whole, since a cut through a part would void its verification.
    public sealed class KeepFirstParent : ICrossover
    {
        public Network Cross(Network firstParent, Network secondParent, Random random) => firstParent;
    }

    // A glue-only edit may drop a port's synapses to glue but never add one, so glue cannot grow new reads of a part.
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
