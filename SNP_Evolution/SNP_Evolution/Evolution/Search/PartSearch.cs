using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Genome;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Evolution.Verification;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution.Search
{
    // How a part is evolved for one contract. Budget is search evaluations; ShrinkBudget is spent after a part is found
    // making it smaller. ExtraNeurons is how far past the port neurons a network may grow. HardwareProfile keeps every
    // network searched and shrunk within the hardware profile. RobustJitter, when above 0, shrinks over cells of robustness
    // at that jitter and keeps the most robust part that verifies, the cheapest among equally robust ones.
    public sealed record PartSearchSettings(
        long Budget,
        long ShrinkBudget,
        int Population,
        EvolutionSearch Algorithm,
        Func<ISimulationEngine> CreateEngine,
        bool Lexicase = true,
        int ExtraNeurons = 4,
        int MaxDelay = 3,
        int MaxInitialSpikes = 4,
        int MaxProduce = 2,
        bool HardwareProfile = false,
        int RobustJitter = 0)
    {
        public static PartSearchSettings Default { get; } = new PartSearchSettings(50_000, 50_000 / 4, 60, SearchCatalog.StructuralDefault, () => new ExhaustiveCpuEngine());
    }

    public sealed record MeasuredPart(Part Part, PartMeasurement Measurement);

    // Spent is what the search, shrinking and verification cost; Evaluations counts the networks scored and checked,
    // which is what a part's record holds.
    public sealed record PartOutcome(Contract Contract, int Seed, BudgetReport Spent, Part? Part, PartMeasurement? Measurement)
    {
        public bool Solved => Part != null;

        public long Evaluations => Spent.NetworksAndChecks;
    }

    // Evolves a part from scratch for a contract, verifies it on the exhaustive engine, then shrinks it, keeping the
    // cheapest network that still verifies. The search and the shrink are each a phase of the request's budget, limited
    // by the settings' budgets.
    public sealed class PartSearch : ISearch<MeasuredPart>
    {
        private const float MutationRate = 0.5f;
        private const int CheckedPerGeneration = 3;

        private readonly PartSearchSettings settings;

        public PartSearch(PartSearchSettings? settings = null)
        {
            this.settings = settings ?? PartSearchSettings.Default;
        }

        public string Name => "Part search: evolve, verify and shrink a part for a contract";

        // A seed of the contract's own, so running a subset of contracts gives each the part a full run would.
        public static int SeedFor(int runSeed, string contractName)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char letter in contractName)
                {
                    hash = (hash ^ letter) * 16777619;
                }
                return (int)(hash ^ (uint)runSeed * 2654435761) & int.MaxValue;
            }
        }

        public static PartOutcome Evolve(Contract contract, int runSeed, PartSearchSettings settings, EvaluationBudget budget, Action<string> log)
        {
            int seed = SeedFor(runSeed, contract.Name);
            SearchOutcome<MeasuredPart> outcome = new PartSearch(settings).Run(new SearchRequest<MeasuredPart>(new ContractTask(contract), budget, new Random(seed), log));
            return new PartOutcome(contract, seed, outcome.Spent, outcome.Best?.Part, outcome.Best?.Measurement);
        }

        public SearchOutcome<MeasuredPart> Run(SearchRequest<MeasuredPart> request)
        {
            ContractTask task = request.Task is IContractTask contractTask ? ContractTask.Of(contractTask) : throw new ArgumentException("A part search needs a contract.", nameof(request));
            Contract contract = task.Contract;
            var space = new GenomeSpace(
                InputCount: task.InputCount,
                RuleForm: RuleForm.Standard,
                MinNeurons: task.Binding.NeuronsNeeded,
                MaxNeurons: task.Binding.NeuronsNeeded + settings.ExtraNeurons,
                MaxDelay: settings.MaxDelay,
                MaxInitialSpikes: settings.MaxInitialSpikes,
                MaxProduce: settings.MaxProduce,
                HardwareProfile: settings.HardwareProfile);
            var factory = new NetworkFactory(space, new ExpressionGenerator(ExpressionGenerator.ExperimentalTemplates, 4, request.Random), request.Random);
            var verifier = new Verifier(task, request.Budget);
            var setup = new NetworkSetup(settings.Population, MutationRate, factory, factory.NewNetwork, new NetworkScoring(settings.CreateEngine, verifier.Options, SolvedRetests: 3))
            {
                Lexicase = settings.Lexicase,
            };
            EvaluationBudget searchPhase = request.Budget.Phase(settings.Budget);
            IGeneticAlgorithm algorithm = settings.Algorithm.Create(setup.Context(setup.Scoring.Evaluator(task, searchPhase, request.Random), request.Random, _ => { }));
            PartMeasurement? found = null;
            (SearchStop stop, _) = GenerationLoop.Run(algorithm, int.MaxValue, () => searchPhase.IsSpent, run =>
                (found = run.Population.Where(individual => Solved.Solves(individual.Fitness, task)).Take(CheckedPerGeneration).Select(individual => verifier.Measure(individual.Genes))
                    .FirstOrDefault(measured => measured.Verdict is Verdict.Passed)) != null);
            if (found == null)
            {
                string failing = string.Join("; ", (algorithm.Best?.Description ?? "").Split(Environment.NewLine).Take(3));
                request.Log($"{contract.Name}: not solved in {searchPhase.Networks} evaluations (best fitness {algorithm.Best?.Fitness ?? 0:0.000}, failing {failing}).");
                return new SearchOutcome<MeasuredPart>(stop, null, algorithm.Best?.Fitness ?? 0, algorithm.Best?.Description ?? "", request.Budget.Report());
            }
            request.Log($"{contract.Name}: solved after {searchPhase.Networks} evaluations, {found.Cost}.");
            Individual start = algorithm.Population.First(individual => ReferenceEquals(individual.Genes, found.Network));
            PartMeasurement kept = found;
            bool Verifies(Network network)
            {
                if (verifier.Measure(network) is not { Verdict: Verdict.Passed } passed)
                {
                    return false;
                }
                kept = passed;
                return true;
            }
            new ShrinkSearch(settings.RobustJitter).Run(
                new SearchRequest<Individual>(task, request.Budget.Phase(settings.ShrinkBudget), request.Random, _ => { }) { Seeds = new[] { start }, Networks = setup }, Verifies);
            request.Log($"{contract.Name}: kept {kept.Cost}, latency {kept.Latency}.");
            return new SearchOutcome<MeasuredPart>(SearchStop.Solved, new MeasuredPart(new Part(contract, kept.Network, task.Binding), kept), 1, kept.Description, request.Budget.Report());
        }
    }
}
