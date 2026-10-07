using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution.Verification
{
    // Checks a network against a contract on the exhaustive engine: the one place a network is run on a contract's cases
    // and judged. Part search, promotion, the part library and each bound of a bounded check go through it. A case too
    // wide to follow exactly gives no verdict, since sampling is not proof. Every network run is charged to the budget
    // as one exhaustive check.
    public sealed class Verifier
    {
        public const int MaxConfigurations = 50_000;

        // The runs a case too wide to follow is sampled with instead, which a search scoring on the same cases uses too.
        public const int Repetitions = 20;

        private readonly ExhaustiveCpuEngine engine;
        private readonly EvaluationBudget budget;

        public Verifier(ContractTask task, EvaluationBudget budget, int maxConfigurations = MaxConfigurations)
        {
            Task = task;
            this.budget = budget;
            engine = new ExhaustiveCpuEngine(maxConfigurations);
            Options = new SimulationOptions(task.StepsNeeded, Repetitions, OutputTiming.Interval);
        }

        public ContractTask Task { get; }

        public SimulationOptions Options { get; }

        public static PartMeasurement Measure(Part part, EvaluationBudget budget) => new Verifier(part.Task(), budget).Measure(part.Network);

        public IReadOnlyList<TrialResult> Run(Network network)
        {
            budget.Charge(EvaluationKind.ExhaustiveCheck, 1);
            return engine.Run(Task.Cases.Select(@case => @case.Of(network)).ToList(), Options, new Random(0));
        }

        public Verdict Check(Network network) => Judge(Run(network));

        // Unknown at the first case not followed exactly; otherwise failed at the first check that is not met on every
        // computation, with the first computation that fails it.
        public Verdict Judge(IReadOnlyList<TrialResult> results)
        {
            int inexact = results.ToList().FindIndex(result => !result.Exact);
            if (inexact >= 0)
            {
                return new Verdict.Unknown(new StopReason(Stop.TooWide, Counterexample.InputsOf(Task.Contract, Task.Contract.Cases[inexact])));
            }
            int failing = Task.Checks(results).ToList().FindIndex(score => score < 1);
            if (failing < 0)
            {
                return new Verdict.Passed();
            }
            int caseIndex = failing / ContractTask.RuleCount;
            // A failing check always has a failing computation; the first is only a fallback the types ask for.
            PortRun run = Task.FailingRun(results, failing) ?? results[caseIndex].PortRuns[0];
            return new Verdict.Failed(new Counterexample(Task.Contract, Task.Contract.Cases[caseIndex], (ContractRule)(failing % ContractTask.RuleCount),
                Task.Reading(run, caseIndex), Task.Latency(run, caseIndex), run));
        }

        public PartMeasurement Measure(Network network)
        {
            IReadOnlyList<TrialResult> results = Run(network);
            Verdict verdict = Judge(results);
            string description = verdict is Verdict.Unknown ? "some cases have too many computations to follow exactly" : Task.Describe(results);
            return new PartMeasurement(network, HardwareCost.Of(network, results), Task.Niche(results)?.Item2 ?? 0, Task.Behaviour(results), verdict, description);
        }
    }
}
