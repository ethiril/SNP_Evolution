using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Genome;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Search
{
    // With probability rate, makes one edit chosen by weight, and keeps making more with probability repeatChance,
    // so most children are one small step away while a few take a bigger leap. Under pressure every child is mutated
    // and gets that many extra edits on top. A tracker is told which network each changed child came from.
    public sealed class WeightedMutation : IMutation
    {
        private readonly float rate;
        private readonly IReadOnlyList<WeightedEdit> edits;
        private readonly double repeatChance;
        private readonly double totalWeight;
        private readonly MutationPressure? pressure;
        private readonly ModuleTracker? tracker;

        public WeightedMutation(float rate, IReadOnlyList<WeightedEdit> edits, double repeatChance = 0.3, MutationPressure? pressure = null, ModuleTracker? tracker = null)
        {
            this.rate = rate;
            this.edits = edits;
            this.repeatChance = repeatChance;
            this.pressure = pressure;
            this.tracker = tracker;
            totalWeight = edits.Sum(edit => edit.Weight);
        }

        public IReadOnlyList<WeightedEdit> Edits => edits;

        // Every edit, weighted towards small changes to the rules over changes to the structure. With modules, copies
        // of library modules can be put in and freed, and when they are frozen no other edit changes their insides.
        public static WeightedMutation Structural(float rate, NetworkFactory factory, MutationPressure? pressure = null, ModuleSupport? modules = null)
        {
            var edits = new List<WeightedEdit>
            {
                new WeightedEdit("Nudge expression", new NudgeExpression(), 4),
                new WeightedEdit("Replace expression", new ReplaceExpression(factory), 2),
                new WeightedEdit("Nudge rule", new NudgeRule(factory.Space), 3),
                new WeightedEdit("Nudge initial spikes", new NudgeInitialSpikes(), 2),
                new WeightedEdit("Add rule", new AddRule(factory), 1),
                new WeightedEdit("Remove rule", new RemoveRule(), 1),
                new WeightedEdit("Add synapse", new AddSynapse(), 1.5),
                new WeightedEdit("Remove synapse", new RemoveSynapse(), 1.5),
                new WeightedEdit("Split synapse", new SplitSynapse(factory), 0.5),
                new WeightedEdit("Add neuron", new AddNeuron(factory), 0.5),
                new WeightedEdit("Remove neuron", new RemoveNeuron(factory.Space), 0.75),
            };
            if (factory.Space.DuplicateNeurons)
            {
                edits.Add(new WeightedEdit("Duplicate neuron", new DuplicateNeuron(factory.Space), 0.25));
            }
            if (factory.Space.RuleForm == RuleForm.Mixed)
            {
                edits.Add(new WeightedEdit("Switch rule form", new SwitchRuleForm(), 0.5));
            }
            if (modules != null)
            {
                if (modules.Freeze)
                {
                    edits = edits.Select(edit => edit with { Edit = new ProtectModules(edit.Edit) }).ToList();
                }
                edits.Add(new WeightedEdit("Insert module", new InsertModule(modules.Library, factory.Space), 0.75));
                edits.Add(new WeightedEdit("Dissolve module", new DissolveModule(), 0.1));
                // Only with contract parts, so a run with harvested modules alone draws its edits as before.
                if (modules.Library.Parts.Count > 0)
                {
                    edits.Add(new WeightedEdit("Rewire port", new RewirePort(modules.Library), 0.5));
                    edits.Add(new WeightedEdit("Add glue neuron", new AddGlueNeuron(modules.Library, factory), 0.25));
                    edits.Add(new WeightedEdit("Swap part", new SwapPart(modules.Library), 0.1));
                }
            }
            return new WeightedMutation(rate, edits, pressure: pressure, tracker: modules?.Tracker);
        }

        public Network Mutate(Network network, Random random)
        {
            int extraEdits = pressure?.ExtraEdits ?? 0;
            if (extraEdits == 0 && random.NextDouble() >= rate)
            {
                return network;
            }
            Network child = network;
            do
            {
                child = Choose(random).Mutate(child, random);
            }
            while (random.NextDouble() < repeatChance);
            for (int edit = 0; edit < extraEdits; edit++)
            {
                child = Choose(random).Mutate(child, random);
            }
            if (child != network)
            {
                tracker?.RecordChild(network, child);
            }
            return child;
        }

        private IMutation Choose(Random random)
        {
            double remaining = random.NextDouble() * totalWeight;
            foreach (WeightedEdit edit in edits)
            {
                if (remaining < edit.Weight)
                {
                    return edit.Edit;
                }
                remaining -= edit.Weight;
            }
            return edits[^1].Edit;
        }
    }
}
