using SnpEvolution.Cli;
using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Genome;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Proposals;
using SnpEvolution.Evolution.Search;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Simulation;
using Xunit.Abstractions;
using static SnpEvolution.Tests.Evolution.ModuleFixtures;

namespace SnpEvolution.Tests.Evolution
{
    public class ProposalTests
    {
        private readonly ITestOutputHelper output;

        public ProposalTests(ITestOutputHelper output) => this.output = output;

        private static IReadOnlyList<string> Parts(ShapeProposal? proposal) => proposal!.Contracts.Select(contract => contract.Name).Order().ToList();

        [Fact]
        public void FibonacciGapsAskForTwoRegistersAndAnAdd() =>
            Assert.Equal(new[] { "add", "register", "register" }, Parts(RecurrenceProposer.Propose(new[] { 1, 1, 2, 3, 5, 8, 13, 21 })));

        [Fact]
        public void PowersOfTwoAskForARegisterAndADouble() =>
            Assert.Equal(new[] { "double", "register" }, Parts(RecurrenceProposer.Propose(new[] { 1, 2, 4, 8, 16, 32 })));

        [Fact]
        [Slow]
        public void ARandomSequenceAsksForNothing()
        {
            var random = new Random(31);
            for (int attempt = 0; attempt < 20; attempt++)
            {
                int[] gaps = Enumerable.Range(0, 10).Select(_ => random.Next(1, 40)).ToArray();

                Assert.Null(RecurrenceProposer.Propose(gaps));
            }
        }

        // 2, 4, 6, 8 fits no linear recurrence with coefficients of 0 or more, but its gaps grow by 2 each time.
        [Fact]
        public void GapsWithAConstantDifferenceAskForARegisterAndAnAdd()
        {
            ShapeProposal proposal = RecurrenceProposer.Propose(new[] { 2, 4, 6, 8, 10 })!;

            Assert.Equal(new[] { "add", "register" }, Parts(proposal));
            Assert.Equal("every difference of the gaps is 2", proposal.Form);
            Assert.Equal(new[] { "increment", "register" }, Parts(RecurrenceProposer.Propose(new[] { 3, 4, 5, 6, 7 })));
            Assert.Null(RecurrenceProposer.Propose(new[] { 10, 8, 6, 4, 2 }));
        }

        [Fact]
        public void AFailingGapProposesATimerAndFailingCasesASubContract()
        {
            var sequence = new SequenceTask("gaps", new[] { 2, 5, 3 });
            var task = new ContractTask(FirstParts.Named("increment"));
            int caseThree = FirstParts.Values.ToList().IndexOf(3);

            Assert.Equal("delay 5", sequence.Propose(new[] { 1, 2 })!.Name);
            Contract sub = task.Propose(new[] { ContractTask.CheckIndex(caseThree, ContractRule.DoneOnce) })!;
            Assert.Equal("increment on n=3", sub.Name);
            Assert.Equal(4, sub.Cases.Single().Outputs["out"]);
            Assert.Null(task.Propose(Enumerable.Range(0, task.Contract.Cases.Count).Select(caseIndex => ContractTask.CheckIndex(caseIndex, ContractRule.OnTime)).ToList()));
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
            var factory = new NetworkFactory(new GenomeSpace(InputCount: 0, RuleForm: RuleForm.Standard, MaxNeurons: 4),
                new ExpressionGenerator(ExpressionGenerator.ExperimentalTemplates, 4, random), random);
            var evaluator = new FitnessEvaluator(new SequentialCpuEngine(), task, new SimulationOptions(30, 2, OutputTiming.Interval), 1, random);
            var space = new CompositionSpace(library, factory, new CompositionMix(), random);
            var context = new EvolutionContext(12, 0.5f, random, space.NewNetwork, evaluator, factory, _ => { }, Parts: library);
            IGeneticAlgorithm search = AlgorithmCatalog.All.First(choice => AlgorithmCatalog.IsComposition(choice.Name)).Create(context);
            var settings = new PartSearchSettings(30_000, 0, PartsSession.Population, Catalog.ChoiceFor(Catalog.StructuralDefault), () => new ExhaustiveCpuEngine());
            var log = new List<string>();
            var proposals = new PartProposals(search, space, () => task, contract => PartEvolution.Evolve(contract, 1, settings, log.Add),
                new ProposalPolicy(Patience: 2, MaxProposals: 1), 12, log.Add);

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
