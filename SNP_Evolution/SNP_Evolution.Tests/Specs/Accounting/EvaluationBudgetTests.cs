using SnpEvolution.Application;
using SnpEvolution.Search;
using SnpEvolution.Search.Benchmarking;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Verification;

namespace SnpEvolution.Tests.Specs.Accounting
{
    public class EvaluationBudgetTests
    {
        // An evaluator reuses the same case objects for every network, so trials sharing the first trial's input are one per network.
        private sealed class CountingEngine : ISimulationEngine
        {
            private readonly ISimulationEngine inner;
            private long networks;

            public CountingEngine(ISimulationEngine inner) => this.inner = inner;

            public long Networks => Interlocked.Read(ref networks);

            public EngineSupport Support => inner.Support;

            public IReadOnlyList<TrialResult> Run(IReadOnlyList<Trial> trials, SimulationOptions options, Random random)
            {
                Interlocked.Add(ref networks, trials.Count(trial => ReferenceEquals(trial.Input, trials[0].Input)));
                return inner.Run(trials, options, random);
            }
        }

        [Fact]
        public void APhaseChargesItsParentAndStopsAtItsOwnLimit()
        {
            var run = new EvaluationBudget(limit: 100);
            EvaluationBudget phase = run.Phase(10, EvaluationSource.Proposals);

            phase.Charge(EvaluationKind.Network, 10, EvaluationSource.Main);
            phase.Charge(EvaluationKind.ExhaustiveCheck, 3);

            Assert.True(phase.IsSpent);
            Assert.False(run.IsSpent);
            Assert.Equal(10, run[EvaluationSource.Proposals]);
            Assert.Equal(0, run[EvaluationSource.Main]);
            Assert.Equal(3, run[EvaluationKind.ExhaustiveCheck]);
        }

        [Fact]
        public void OnlyNetworkEvaluationsCountTowardsTheLimit()
        {
            var budget = new EvaluationBudget(limit: 5);

            budget.Charge(EvaluationKind.ExhaustiveCheck, 50);
            budget.Charge(EvaluationKind.ProofStep, 50);
            budget.Charge(EvaluationKind.InterpreterRun, 50);
            budget.Charge(EvaluationKind.JitterRun, 50);
            Assert.False(budget.IsSpent);

            budget.Charge(EvaluationKind.Network, 5);
            Assert.True(budget.IsSpent);
        }

        [Fact]
        public void WithoutALimitNothingIsSpent()
        {
            var budget = new EvaluationBudget();
            budget.Charge(EvaluationKind.Network, long.MaxValue / 2);

            Assert.False(budget.IsSpent);
            Assert.True(new EvaluationBudget().Phase(0).IsSpent);
        }

        [Fact]
        public void TheReportNamesOtherKindsOnlyWhenSomeWereSpent()
        {
            var budget = new EvaluationBudget();
            budget.Charge(EvaluationKind.Network, 2_500);
            Assert.Equal("Evaluations: 2,500 main run, 0 side runs, 0 incubation, 0 verification, 0 proposed parts; 2,500 in all.", budget.Report().Describe());

            budget.Charge(EvaluationKind.ExhaustiveCheck, 7);
            Assert.EndsWith(" Besides them, 7 exhaustive checks.", budget.Report().Describe());
        }

        [Fact]
        public void AVerifierChargesOneExhaustiveCheckPerNetworkItRuns()
        {
            var budget = new EvaluationBudget();

            Verifier.Measure(ReferenceParts.Delay(2), budget);

            Assert.Equal(1, budget[EvaluationKind.ExhaustiveCheck]);
            Assert.Equal(0, budget.Networks);
        }

        [Fact]
        [Slow]
        public void EveryNetworkAModularRunScoresIsCharged()
        {
            var engine = new CountingEngine(new SequentialCpuEngine());
            var settings = new Settings
            {
                Target = new OutputTarget(TargetKind.Sequence, new[] { 1, 1, 2, 3, 5, 8, 13, 21, 34 }),
                Task = Catalog.TargetTask,
                Engine = new CatalogEntry<Settings, ISimulationEngine>("counting", _ => engine),
                Repetitions = 2,
                PopulationSize = 10,
                MaxGenerations = 60,
                IterativeEvolution = false,
                StagnationPatience = 10,
                Modules = true,
            };
            var budget = new EvaluationBudget();

            EvolutionSession.Evolve(settings, settings.SelectedTask, factory => factory.NewNetwork(), new Random(3), _ => { }, budget);

            Assert.True(budget[EvaluationSource.SideRun] > 0);
            Assert.Equal(engine.Networks, budget.Networks);
        }

        [Fact]
        [Slow]
        public void EveryNetworkABenchmarkRunScoresIsChargedRetestsIncluded()
        {
            var engine = new CountingEngine(new ExhaustiveCpuEngine());
            BenchmarkSettings settings = BenchmarkSettings.Default with { Seeds = 1, CreateEngine = () => engine };

            RunOutcome outcome = Benchmark.RunOnce(Catalog.StructuralDefault, TaskSuite.Functions.First(task => task.Name == "Compute n"), seed: 1, budget: 4000, settings);

            Assert.True(outcome.Solved);
            Assert.Equal(engine.Networks, outcome.Evaluations);
        }

        [Fact]
        [Slow]
        public void EveryNetworkAPartSearchScoresIsChargedAndItsChecksBesideThem()
        {
            var engine = new CountingEngine(new ExhaustiveCpuEngine());
            var settings = new PartSearchSettings(5_000, 1_000, 30, Catalog.StructuralDefault, () => engine);

            PartOutcome outcome = PartSearch.Evolve(PartFixtures.DelayContract(2), 1, settings, new EvaluationBudget(), _ => { });

            Assert.True(outcome.Solved);
            Assert.Equal(engine.Networks, outcome.Spent.Networks);
            Assert.True(outcome.Spent[EvaluationKind.ExhaustiveCheck] > 0);
            Assert.Equal(outcome.Spent.Networks + outcome.Spent[EvaluationKind.ExhaustiveCheck], outcome.Evaluations);
        }
    }
}
