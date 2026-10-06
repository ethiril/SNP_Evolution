using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Proposals
{
    // MaxProposals is low because each proposal costs a whole part evolution.
    public sealed record ProposalPolicy(int Patience = 25, int MaxProposals = 6);

    public enum ProposalSource
    {
        // The checks no network passes.
        FailingChecks,

        // A form fitted to the target sequence.
        TargetShape,
    }

    public enum ProposalOutcome
    {
        Solved,
        NotSolved,

        // The library already has a part for the contract, so nothing was evolved.
        AlreadyKept,
    }

    public sealed record Proposal(int Generation, Contract Contract, ProposalSource Source, string Reason, ProposalOutcome Outcome, long Evaluations, int? Module)
    {
        public override string ToString() =>
            $"generation {Generation}: {Contract.Name} ({(Source == ProposalSource.TargetShape ? "target shape" : "failing checks")}, {Reason}): " +
            Outcome switch
            {
                ProposalOutcome.Solved => $"solved in {Evaluations} evaluations, kept as module {Module}",
                ProposalOutcome.NotSolved => $"not solved in {Evaluations} evaluations",
                _ => $"already in the library as module {Module}",
            };
    }

    // A proposed part is verified on the exhaustive engine like every first part, since one admitted on a weaker standard would poison every composition using it.
    public sealed class PartProposals : IGeneticAlgorithm
    {
        private const float ImprovementTolerance = 1e-6f;

        private readonly IGeneticAlgorithm inner;
        private readonly CompositionSpace space;
        private readonly Func<ITask> currentTask;
        private readonly Func<Contract, PartOutcome> solve;
        private readonly ProposalPolicy policy;
        private readonly int populationSize;
        private readonly Action<string> log;
        private readonly List<Proposal> proposals = new List<Proposal>();
        private readonly HashSet<string> asked = new HashSet<string>();
        private bool shapeRead;
        private float? bestFitness;
        private int stale;

        public PartProposals(IGeneticAlgorithm inner, CompositionSpace space, Func<ITask> currentTask, Func<Contract, PartOutcome> solve, ProposalPolicy policy,
            int populationSize, Action<string> log)
        {
            this.inner = inner;
            this.space = space;
            this.currentTask = currentTask;
            this.solve = solve;
            this.policy = policy;
            this.populationSize = populationSize;
            this.log = log;
        }

        public IGeneticAlgorithm Inner => inner;

        public ModuleLibrary Library => space.Library;

        public IReadOnlyList<Proposal> Proposals => proposals;

        public long Evaluations => proposals.Sum(proposal => proposal.Evaluations);

        public IReadOnlyList<Individual> Population => inner.Population;

        public int Generation => inner.Generation;

        public Individual? Best => inner.Best;

        public IReadOnlyList<IReadOnlyList<float>> FitnessHistory => inner.FitnessHistory;

        public void NextGeneration()
        {
            inner.NextGeneration();
            if (inner.Best is not Individual best || !GeneticAlgorithm.IsRecordableFitness(best.Fitness))
            {
                return;
            }
            if (bestFitness == null || best.Fitness > bestFitness + ImprovementTolerance)
            {
                bestFitness = best.Fitness;
                stale = 0;
                return;
            }
            if (++stale < policy.Patience)
            {
                return;
            }
            stale = 0;
            React();
        }

        public void Immigrate(IReadOnlyList<Network> newcomers) => inner.Immigrate(newcomers);

        public void Rescore()
        {
            inner.Rescore();
            bestFitness = null;
            stale = 0;
        }

        public string Describe() => proposals.Count == 0
            ? "No parts were proposed."
            : $"{proposals.Count} part(s) proposed, {proposals.Count(proposal => proposal.Outcome == ProposalOutcome.Solved)} solved, {Evaluations} evaluations spent:{Environment.NewLine}"
                + string.Join(Environment.NewLine, proposals.Select(proposal => "  " + proposal));

        private void React()
        {
            foreach ((Contract contract, ProposalSource source, string reason) in Candidates(currentTask()))
            {
                if (proposals.Count >= policy.MaxProposals || !asked.Add(contract.Name))
                {
                    continue;
                }
                Propose(contract, source, reason);
            }
        }

        // The target's shape is read only once, since it does not change while the run stalls.
        private List<(Contract Contract, ProposalSource Source, string Reason)> Candidates(ITask task)
        {
            var candidates = new List<(Contract Contract, ProposalSource Source, string Reason)>();
            if (!shapeRead && task is SequenceTask sequence)
            {
                shapeRead = true;
                if (RecurrenceProposer.Propose(sequence.Expected) is ShapeProposal shape)
                {
                    log($"The target's shape fits {shape.Form}, which asks for {shape.Parts}.");
                    candidates.AddRange(shape.Contracts.DistinctBy(contract => contract.Name).Select(contract => (contract, ProposalSource.TargetShape, shape.Form)));
                }
                else
                {
                    log("No recurrence or constant difference fits the target exactly, so its shape proposes nothing.");
                }
            }
            CheckDiagnosis diagnosis = CheckDiagnosis.Of(inner.Population);
            if (task.Propose(diagnosis.Unsolved) is Contract failing)
            {
                candidates.Add((failing, ProposalSource.FailingChecks, diagnosis.Frontier is int frontier ? $"nothing passes {task.CheckName(frontier)}" : "unpassed checks"));
            }
            return candidates;
        }

        private void Propose(Contract contract, ProposalSource source, string reason)
        {
            ModuleLibrary library = space.Library;
            if (library.PartFor(contract.Name) is Module kept)
            {
                Record(new Proposal(inner.Generation, contract, source, reason, ProposalOutcome.AlreadyKept, 0, kept.Id));
                return;
            }
            if (contract.Problems().Count > 0)
            {
                log($"Proposed contract {contract.Name} is malformed, so it is dropped: {string.Join(" ", contract.Problems())}");
                return;
            }
            log($"Proposing a part for {contract.Name} ({reason}).");
            PartOutcome outcome = solve(contract);
            if (outcome.Part is not Part part || outcome.Measurement is not PartMeasurement measurement)
            {
                Record(new Proposal(inner.Generation, contract, source, reason, ProposalOutcome.NotSolved, outcome.Evaluations, null));
                return;
            }
            string origin = $"a proposal from {(source == ProposalSource.TargetShape ? "the target's shape" : "failing checks")}";
            Module module = library.AddPart(LibraryPart.Of(part, measurement, new PartOrigin(outcome.Seed, origin, outcome.Evaluations)), origin);
            Record(new Proposal(inner.Generation, contract, source, reason, ProposalOutcome.Solved, outcome.Evaluations, module.Id));
            GiveToBestNetworks(module);
        }

        private void GiveToBestNetworks(Module module)
        {
            List<Network> composites = Ranking.Rank(inner.Population.Where(individual => individual.IsEvaluated))
                .Take(Math.Max(1, populationSize / 4))
                .Select(individual => space.WithPart(individual.Genes, module))
                .Where(network => network != null)
                .Select(network => network!)
                .ToList();
            if (composites.Count > 0)
            {
                inner.Immigrate(composites);
                log($"{composites.Count} of the best networks get a copy of module {module.Id} to wire in.");
            }
        }

        private void Record(Proposal proposal)
        {
            proposals.Add(proposal);
            log("Proposal " + proposal);
        }
    }
}
