using SnpEvolution.Search;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Search.Genome;
using SnpEvolution.Search.Modules;
using SnpEvolution.Search.Proposals;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;
using Xunit.Abstractions;
using static SnpEvolution.Tests.Fixtures.ModuleFixtures;

namespace SnpEvolution.Tests.Search.Proposals
{
    public class PartProposalsTests
    {
        // The population evolve-parts searches for a part with.
        private const int PartSearchPopulation = 60;

        private readonly ITestOutputHelper output;

        public PartProposalsTests(ITestOutputHelper output) => this.output = output;

        // A composition search over a library holding only an increment.
        private static (ModuleLibrary Library, CompositionSpace Space, IGeneticAlgorithm Search) IncrementOnlyRun(ITask task, Random random)
        {
            var library = new ModuleLibrary();
            library.AddPart(PartFixtures.Verified(ReferenceParts.Increment()), "a test");
            var factory = Factories.Networks(new GenomeSpace(InputCount: 0, RuleForm: RuleForm.Standard, MaxNeurons: 4), random);
            var evaluator = Runs.SamplingEvaluator(task, random, maxSteps: 30);
            var space = new CompositionSpace(library, factory, new CompositionMix(), random);
            IGeneticAlgorithm search = SearchCatalog.CompositionMapElites.Create(new EvolutionContext(12, 0.5f, random, space.NewNetwork, evaluator, factory, _ => { }, Parts: library));
            return (library, space, search);
        }

        // Fibonacci gaps fit a recurrence, so the target's shape asks for its parts before any check is read.
        [Fact]
        public void ASequenceTargetProposesThePartsItsShapeAsksFor()
        {
            var task = new SequenceTask("fibonacci", new[] { 1, 1, 2, 3, 5, 8 });
            var (_, space, search) = IncrementOnlyRun(task, new Random(3));
            var proposals = new PartProposals(search, space, () => task, contract => new PartOutcome(contract, 0, new EvaluationBudget().Report(), null, null),
                new ProposalPolicy(Patience: 2, MaxProposals: 6), new EvaluationBudget(), 12, _ => { });

            for (int generation = 0; generation < 12 && proposals.Proposals.Count == 0; generation++)
            {
                proposals.NextGeneration();
            }

            Assert.Contains(proposals.Proposals, proposal => proposal.Source == ProposalSource.TargetShape);
        }

        // An increment alone cannot make a sequence of gaps, so the run stalls at once and the missed gap is proposed as a delay.
        [Fact]
        public void AStalledRunProposesAPartSolvesItAndAddsIt()
        {
            var task = new SequenceTask("gaps", new[] { 1, 6, 1 });
            var (library, space, search) = IncrementOnlyRun(task, new Random(3));
            var settings = new PartSearchSettings(30_000, 0, PartSearchPopulation, SearchCatalog.StructuralDefault, () => new ExhaustiveCpuEngine());
            var log = new List<string>();
            var proposals = new PartProposals(search, space, () => task, contract => PartSearch.Evolve(contract, 1, settings, new EvaluationBudget(), log.Add),
                new ProposalPolicy(Patience: 2, MaxProposals: 1), new EvaluationBudget(), 12, log.Add);

            for (int generation = 0; generation < 12 && proposals.Proposals.Count == 0; generation++)
            {
                proposals.NextGeneration();
            }

            output.WriteLine(proposals.Describe());
            Proposal proposal = Assert.Single(proposals.Proposals);
            Assert.Equal(ProposalSource.FailingChecks, proposal.Source);
            Assert.Equal(ProposalOutcome.Solved, proposal.Outcome);
            Assert.StartsWith("delay ", proposal.Contract.Name);
            LibraryPart proposed = library.Find(proposal.Module!.Value)!.Part!;
            Assert.Equal(proposal.Contract.Name, proposed.Contract.Name);
            Assert.True(proposed.Proven?.AllInputs);
            Assert.Contains(log, line => line.StartsWith("Proposal generation") && line.Contains("solved in"));
            Assert.Contains("1 solved", proposals.Describe());
        }
    }
}
