using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Model;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Search.Genome;
using SnpEvolution.Search.Operators;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;
using SnpEvolution.Specs.Verification;

namespace SnpEvolution.Search
{
    // After STOKE (Schkufza, Sharma and Aiken 2013): the seed is taken to solve the task, and only a cheaper network that passes the check replaces it.
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

        // A check sees only the task, so under the hardware profile or a deterministic space the edits must keep every
        // network within it themselves.
        internal static (ICrossover Crossover, IMutation Edits) Operators(NetworkFactory factory) =>
            factory.Space.Conforms
                ? (new ConformingCrossover(new NeuronCrossover(), factory.Space), new ConformingMutation(Edits(factory), factory.Space))
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
                if (archive.Population
                    .Where(individual => Solved.Solves(individual.Fitness, request.Task) && Compare(individual.Genes, kept.Genes) < 0)
                    .OrderBy(individual => individual.Genes, Comparer<Network>.Create(Compare))
                    .Take(CheckedPerGeneration)
                    .FirstOrDefault(candidate => tried.Add(NetworkNotation.Format(candidate.Genes)) && passes(candidate.Genes)) is Individual cheaper)
                {
                    kept = cheaper;
                    request.Log($"Shrink generation {generation}: {Describe(kept.Genes)}.");
                }
                return false;
            });
            return new SearchOutcome<Individual>(stop, kept, kept.Fitness, kept.Description, request.Budget.Report()) { Generations = generations, History = archive.FitnessHistory };
        }
    }
}
