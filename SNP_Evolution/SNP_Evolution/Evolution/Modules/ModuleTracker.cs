using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Modules
{
    // Remembers which network each child was made from and what every network scored, so that when a child is
    // scored it can credit the modules newly put into it, and when a child sets a new best and gets a check right that
    // its parent did not, the neurons that changed, with their neighbours, are kept as a module. This is adaptive
    // representation through learning (Rosca and Ballard 1996): new parts come from the changes that just paid off.
    // For a task without checks, a new best is enough.
    public sealed class ModuleTracker
    {
        public const int MaxHarvestedNeurons = 6;
        private const float Improvement = 1e-4f;

        private readonly ModuleLibrary library;
        private readonly bool harvest;
        private readonly ConditionalWeakTable<Network, Network> parents = new ConditionalWeakTable<Network, Network>();
        private readonly ConditionalWeakTable<Network, FitnessResult> scores = new ConditionalWeakTable<Network, FitnessResult>();
        private float? record;

        public ModuleTracker(ModuleLibrary library, bool harvest = true)
        {
            this.library = library;
            this.harvest = harvest;
        }

        // Called by the mutation for every child it changes.
        public void RecordChild(Network parent, Network child) => parents.AddOrUpdate(child, parent);

        // Scores with the inner evaluator and watches the results.
        public IPopulationEvaluator Watch(IPopulationEvaluator inner) => new Watcher(this, inner);

        // The task changed, so the best so far no longer counts.
        public void Reset() => record = null;

        private void Observe(Network network, FitnessResult result)
        {
            float fitness = Fitness(result);
            if (parents.TryGetValue(network, out Network? parent) && scores.TryGetValue(parent, out FitnessResult? parentResult))
            {
                bool improved = fitness > Fitness(parentResult) + Improvement;
                HashSet<ModuleTag> inherited = ModuleEdits.Instances(parent).Keys.ToHashSet();
                foreach (ModuleTag tag in ModuleEdits.Instances(network).Keys.Where(tag => !inherited.Contains(tag)))
                {
                    library.Credit(tag.Module, improved);
                }
                if (harvest && improved && record is float best && fitness > best + Improvement && DoesMore(result, parentResult)
                    && ModuleCuts.AroundChanges(network, ModuleCuts.Changed(parent, network), MaxHarvestedNeurons) is IReadOnlyList<int> part)
                {
                    library.Add(ModuleCuts.Cut(network, part), $"a change that raised fitness from {Fitness(parentResult):0.000} to {fitness:0.000}");
                }
            }
            scores.AddOrUpdate(network, result);
            if (record is not float current || fitness > current)
            {
                record = fitness;
            }
        }

        private static float Fitness(FitnessResult result) => GeneticAlgorithm.IsRecordableFitness(result.Fitness) ? result.Fitness : -1;

        // Whether the child gets some check right that the parent does not, or the task has no checks.
        private static bool DoesMore(FitnessResult child, FitnessResult parent)
        {
            IReadOnlyList<float> childChecks = child.Checks ?? System.Array.Empty<float>();
            IReadOnlyList<float> parentChecks = parent.Checks ?? System.Array.Empty<float>();
            if (childChecks.Count == 0 || childChecks.Count != parentChecks.Count)
            {
                return childChecks.Count == 0;
            }
            return Enumerable.Range(0, childChecks.Count).Any(check => childChecks[check] >= Solved.Check && parentChecks[check] < Solved.Check);
        }

        private sealed class Watcher : IPopulationEvaluator
        {
            private readonly ModuleTracker tracker;
            private readonly IPopulationEvaluator inner;

            public Watcher(ModuleTracker tracker, IPopulationEvaluator inner)
            {
                this.tracker = tracker;
                this.inner = inner;
            }

            public IReadOnlyList<FitnessResult> EvaluateAll(IReadOnlyList<Network> networks)
            {
                IReadOnlyList<FitnessResult> results = inner.EvaluateAll(networks);
                for (int index = 0; index < networks.Count; index++)
                {
                    tracker.Observe(networks[index], results[index]);
                }
                return results;
            }
        }
    }
}
