using SnpEvolution.Application;
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
        private readonly ITestOutputHelper output;

        public PartProposalsTests(ITestOutputHelper output) => this.output = output;

        // Fibonacci gaps fit a recurrence, so the target's shape asks for its parts before any check is read.
        [Fact]
        public void ASequenceTargetProposesThePartsItsShapeAsksFor()
        {
            var random = new Random(3);
            var library = new ModuleLibrary();
            library.AddPart(Verified(ReferenceParts.Increment()), "a test");
            var task = new SequenceTask("fibonacci", new[] { 1, 1, 2, 3, 5, 8 });
            var factory = Factories.Networks(new GenomeSpace(InputCount: 0, RuleForm: RuleForm.Standard, MaxNeurons: 4), random);
            var evaluator = new FitnessEvaluator(new SequentialCpuEngine(), task, new SimulationOptions(30, 2, OutputTiming.Interval), 1, random, new EvaluationBudget());
            var space = new CompositionSpace(library, factory, new CompositionMix(), random);
            IGeneticAlgorithm search = SearchCatalog.CompositionMapElites.Create(new EvolutionContext(12, 0.5f, random, space.NewNetwork, evaluator, factory, _ => { }, Parts: library));
            var proposals = new PartProposals(search, space, () => task, contract => new PartOutcome(contract, 0, new EvaluationBudget().Report(), null, null),
                new ProposalPolicy(Patience: 2, MaxProposals: 6), new EvaluationBudget(), 12, _ => { });

            for (int generation = 0; generation < 12 && proposals.Proposals.Count == 0; generation++)
            {
                proposals.NextGeneration();
            }

            Assert.Contains(proposals.Proposals, proposal => proposal.Source == ProposalSource.TargetShape);
        }

        // A library of one increment cannot make a sequence of gaps, so the run stalls at once; the gap it misses is
        // proposed as a delay, evolved and verified like a first part, and added to the library.
        [Fact]
        public void AStalledRunProposesAPartSolvesItAndAddsIt()
        {
            var random = new Random(3);
            var library = new ModuleLibrary();
            library.AddPart(Verified(ReferenceParts.Increment()), "a test");
            var task = new SequenceTask("gaps", new[] { 1, 6, 1 });
            var factory = Factories.Networks(new GenomeSpace(InputCount: 0, RuleForm: RuleForm.Standard, MaxNeurons: 4), random);
            var evaluator = new FitnessEvaluator(new SequentialCpuEngine(), task, new SimulationOptions(30, 2, OutputTiming.Interval), 1, random, new EvaluationBudget());
            var space = new CompositionSpace(library, factory, new CompositionMix(), random);
            var context = new EvolutionContext(12, 0.5f, random, space.NewNetwork, evaluator, factory, _ => { }, Parts: library);
            IGeneticAlgorithm search = SearchCatalog.CompositionMapElites.Create(context);
            var settings = new PartSearchSettings(30_000, 0, PartsService.Population, Catalog.StructuralDefault, () => new ExhaustiveCpuEngine());
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
