using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Genome;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Evolution.Verification;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Search
{
    // Evolves a network that already solves the task into cheaper ones that still do, in the spirit of superoptimisers
    // such as STOKE (Schkufza, Sharma and Aiken 2013) and of evolving smaller circuits from a working one (Vasicek and
    // Sekanina). MAP-Elites keeps the best network of every hardware cost cell, every cell starting from the seed, with
    // the usual edits apart from those that only add, plus bypassing and merging neurons. Each generation the cheapest
    // solving elites, up to three, are checked, and the first that passes is kept: on a contract by the Verifier on the
    // exhaustive engine, otherwise by the retests that stop a run. The seed is taken to solve the task already. With a
    // jitter, cells are robustness at that jitter by neuron count, and the most robust part that verifies is kept, the
    // cheapest among equally robust ones.
    public sealed class ShrinkSearch : ISearch<Individual>
    {
        private const int CheckedPerGeneration = 3;

        private readonly int robustJitter;

        public ShrinkSearch(int robustJitter = 0)
        {
            this.robustJitter = robustJitter;
        }

        public string Name => "Shrink, MAP-Elites over hardware cost";

        public bool NeedsSeeds => true;

        // The edits a shrink makes: none that only make a network bigger.
        public static WeightedMutation Edits(NetworkFactory factory)
        {
            var growing = new HashSet<string> { "Add neuron", "Add rule", "Split synapse", "Duplicate neuron" };
            List<WeightedEdit> edits = WeightedMutation.Structural(1, factory).Edits.Where(edit => !growing.Contains(edit.Name)).ToList();
            edits.Add(new WeightedEdit("Bypass neuron", new BypassNeuron(), 1));
            edits.Add(new WeightedEdit("Merge neurons", new MergeNeurons(), 1));
            return new WeightedMutation(1, edits);
        }

        // A check sees only the task, so under the hardware profile the edits must keep every network within it themselves.
        internal static (ICrossover Crossover, IMutation Edits) Operators(NetworkFactory factory) =>
            factory.Space.HardwareProfile
                ? (new ProfileCrossover(new NeuronCrossover()), new ProfileMutation(Edits(factory)))
                : (new NeuronCrossover(), Edits(factory));

        public static string Describe(Network network) =>
            $"{network.Neurons.Count} neurons, {network.RuleCount} rules, {network.SynapseCount} synapses (size {network.Size})";

        public SearchOutcome<Individual> Run(SearchRequest<Individual> request)
        {
            Func<Network, bool> passes;
            if (request.Task is IContractTask contract)
            {
                var verifier = new Verifier(ContractTask.Of(contract), request.Budget);
                passes = network => verifier.Check(network) is Verdict.Passed;
            }
            else
            {
                FitnessEvaluator retests = request.RequireNetworks().Scoring.Evaluator(request.Task, request.Budget, request.Random);
                passes = network => retests.ConfirmSolved(network).Solved;
            }
            return Run(request, passes);
        }

        // Keeps the seed until a cheaper network passes; passes is the check a candidate must pass to be kept.
        public SearchOutcome<Individual> Run(SearchRequest<Individual> request, Func<Network, bool> passes)
        {
            NetworkSetup setup = request.RequireNetworks();
            Individual kept = request.Seeds.Count > 0 ? request.Seeds[0] : throw new ArgumentException("A shrink needs a network to start from.");
            Network start = kept.Genes;
            FitnessEvaluator evaluator = setup.Scoring.Evaluator(request.Task, request.Budget, request.Random);
            (ICrossover crossover, IMutation edits) = Operators(setup.Factory);
            Func<Network, (int, int)> cells = robustJitter > 0 && request.Task is IContractTask robust ? Robustness.Cells(ContractTask.Of(robust), robustJitter, request.Budget) : HardwareCost.Cell;
            var archive = new MapElites(setup.PopulationSize, request.Random, () => start, evaluator, crossover, edits, cells: cells);
            // Robustness in tenths first when it is asked for, then cost; the cells function has measured every network in the archive.
            int Robust(Network network) => robustJitter > 0 ? cells(network).Item1 : 0;
            int Compare(Network first, Network second) =>
                Robust(first) != Robust(second) ? Robust(second).CompareTo(Robust(first)) : HardwareCost.Of(first).CompareTo(HardwareCost.Of(second));
            var tried = new HashSet<string>();
            (SearchStop stop, int generations) = GenerationLoop.Run(request.MaxGenerations, () => request.Budget.IsSpent, request.Cancellation, generation =>
            {
                archive.NextGeneration();
                foreach (Individual candidate in archive.Population
                    .Where(individual => Solved.Solves(individual.Fitness, request.Task) && Compare(individual.Genes, kept.Genes) < 0)
                    .OrderBy(individual => individual.Genes, Comparer<Network>.Create(Compare))
                    .Take(CheckedPerGeneration))
                {
                    if (tried.Add(NetworkNotation.Format(candidate.Genes)) && passes(candidate.Genes))
                    {
                        kept = candidate;
                        request.Log($"Shrink generation {generation}: {Describe(kept.Genes)}.");
                        break;
                    }
                }
                return false;
            });
            return new SearchOutcome<Individual>(stop, kept, kept.Fitness, kept.Description, request.Budget.Report()) { Generations = generations, History = archive.FitnessHistory };
        }
    }
}
