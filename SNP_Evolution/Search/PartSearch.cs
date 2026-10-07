using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Model;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Search.Genome;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;
using SnpEvolution.Specs.Verification;

namespace SnpEvolution.Search
{
    // RobustJitter above 0 makes the shrink keep the most robust part that verifies rather than the cheapest. StagedCases
    // evolves on the cases with the smallest inputs first, adding more each time a stage is solved (see CaseStages).
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
        int RobustJitter = 0,
        bool StagedCases = true)
    {
        public static PartSearchSettings Default { get; } = new PartSearchSettings(50_000, 50_000 / 4, 60, SearchCatalog.StructuralDefault, () => new ExhaustiveCpuEngine());
    }

    public sealed record MeasuredPart(Part Part, PartMeasurement Measurement);

    // Evaluations counts networks scored and checked, which is what a part's record has always held.
    public sealed record PartOutcome(Contract Contract, int Seed, BudgetReport Spent, Part? Part, PartMeasurement? Measurement)
    {
        public bool Solved => Part != null;

        public long Evaluations => Spent.NetworksAndChecks;
    }

    // The search and the shrink are each a phase of the request's budget, limited by the settings' budgets.
    public sealed class PartSearch : ISearch<MeasuredPart>
    {
        private const float MutationRate = 0.5f;
        private const int CheckedPerGeneration = 3;

        // How often a search says how far it has got: once for each of these shares of its budget.
        private const int ProgressReports = 4;

        // The first stage's cases; each later stage doubles them, up to every case.
        public const int FirstStageCases = 3;

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
            NetworkFactory factory = Factory(task, request.Random);
            var verifier = new Verifier(task, request.Budget);
            var setup = new NetworkSetup(settings.Population, MutationRate, factory, factory.NewNetwork, new NetworkScoring(settings.CreateEngine, verifier.Options, SolvedRetests: 3))
            {
                Lexicase = settings.Lexicase,
            };
            EvaluationBudget searchPhase = request.Budget.Phase(settings.Budget);
            IGeneticAlgorithm Create(IPopulationEvaluator evaluator) => settings.Algorithm.Create(setup.Context(evaluator, request.Random, request.Log));
            IReadOnlyList<Stage> stages = settings.StagedCases ? CaseStages(task) : new[] { new Stage(task, task.Contract.Cases.Count) };
            IGeneticAlgorithm algorithm = stages.Count == 1
                ? Create(setup.Scoring.Evaluator(task, searchPhase, request.Random))
                : new IterativeEvolution(stages, task.Contract.Cases.Count, "cases by input size", stageTask => setup.Scoring.Evaluator(stageTask, searchPhase, request.Random), Create,
                    line => request.Log($"{task.Contract.Name}: {line}"));
            PartMeasurement? found = null;
            int reported = 0;
            (SearchStop stop, _) = GenerationLoop.Run(algorithm, int.MaxValue, () => searchPhase.IsSpent, run =>
            {
                // Before the last stage a network solves fewer cases than the contract has, so is not worth verifying.
                bool lastStage = run is not IterativeEvolution staged || staged.Stage == staged.StageCount - 1;
                if (lastStage && (found = FirstVerified(run, task, verifier)) != null)
                {
                    return true;
                }
                // The last share is the outcome's to report.
                for (; reported < ProgressReports - 1 && searchPhase.Networks >= settings.Budget * (reported + 1) / ProgressReports; reported++)
                {
                    request.Log($"{task.Contract.Name}: {searchPhase.Networks} of {settings.Budget} evaluations, best fitness {run.Best?.Fitness ?? 0:0.000}.");
                }
                return false;
            });
            if (found == null)
            {
                string failing = string.Join("; ", (algorithm.Best?.Description ?? "").Split(Environment.NewLine).Take(3));
                string stage = algorithm is IterativeEvolution { IsComplete: false } staged ? $" on stage {staged.Stage + 1} of {staged.StageCount}, {staged.StageLength} cases" : "";
                request.Log($"{task.Contract.Name}: not solved in {searchPhase.Networks} evaluations (best fitness {algorithm.Best?.Fitness ?? 0:0.000}{stage}, failing {failing}).");
                return new SearchOutcome<MeasuredPart>(stop, null, algorithm.Best?.Fitness ?? 0, algorithm.Best?.Description ?? "", request.Budget.Report());
            }
            request.Log($"{task.Contract.Name}: solved after {searchPhase.Networks} evaluations, {found.Cost}.");
            Individual start = algorithm.Population.First(individual => ReferenceEquals(individual.Genes, found.Network));
            PartMeasurement kept = Shrink(request, task, verifier, setup, start, found);
            request.Log($"{task.Contract.Name}: kept {kept.Cost}, latency {kept.Latency}.");
            return new SearchOutcome<MeasuredPart>(SearchStop.Solved, new MeasuredPart(new Part(task.Contract, kept.Network, task.Binding), kept), 1, kept.Description, request.Budget.Report());
        }

        // The cases with the smallest inputs, three at first, then twice as many each stage, ending with every case. A stage
        // with more than three quarters of the cases is left out, since the last stage is hardly longer.
        public static IReadOnlyList<Stage> CaseStages(ContractTask task)
        {
            int total = task.Contract.Cases.Count;
            var stages = new List<Stage>();
            for (int count = FirstStageCases; count * 4 <= total * 3; count *= 2)
            {
                stages.Add(new Stage(task.WithSmallestCases(count), count));
            }
            stages.Add(new Stage(task, total));
            return stages;
        }

        private NetworkFactory Factory(ContractTask task, Random random)
        {
            var space = new GenomeSpace(
                InputCount: task.InputCount,
                RuleForm: RuleForm.Standard,
                MinNeurons: task.Binding.NeuronsNeeded,
                MaxNeurons: task.Binding.NeuronsNeeded + settings.ExtraNeurons,
                MaxDelay: settings.MaxDelay,
                MaxInitialSpikes: settings.MaxInitialSpikes,
                MaxProduce: settings.MaxProduce,
                HardwareProfile: settings.HardwareProfile,
                Deterministic: true);
            return new NetworkFactory(space, new ExpressionGenerator(ExpressionGenerator.ExperimentalTemplates, 4, random), random);
        }

        private static PartMeasurement? FirstVerified(IGeneticAlgorithm run, ContractTask task, Verifier verifier) =>
            run.Population.Where(individual => Solved.Solves(individual.Fitness, task)).Take(CheckedPerGeneration)
                .Select(individual => verifier.Measure(individual.Genes))
                .FirstOrDefault(measured => measured.Verdict is Verdict.Passed);

        private PartMeasurement Shrink(SearchRequest<MeasuredPart> request, ContractTask task, Verifier verifier, NetworkSetup setup, Individual start, PartMeasurement found)
        {
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
            return kept;
        }
    }
}
