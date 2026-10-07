using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Compilation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Search
{
    // Scores a function program on a contract's cases with the interpreter, without building a network. Each case has a
    // check for the right done port, one for each output port (with partial credit when close), one for every register
    // but the outputs back at zero, since a compiled part must end as it began, one for the compiled part being on time,
    // and one for every computation halting.
    // Each check is averaged over the computations of its case, so lexicase can tell programs right on different cases apart.
    public sealed class FunctionScoring : IProgramScoring
    {
        private readonly Contract contract;
        private readonly int maxConfigurations;
        private readonly EvaluationBudget budget;

        public FunctionScoring(Contract contract, int maxConfigurations, EvaluationBudget budget)
        {
            if (!Fits(contract))
            {
                throw new ArgumentException($"Contract '{contract.Name}' has a data port that is not a count, so no register program computes it.", nameof(contract));
            }
            this.contract = contract;
            this.maxConfigurations = maxConfigurations;
            this.budget = budget;
        }

        public int Stage => 0;

        public bool AtLastStage => true;

        public string StageDescription => $"the {contract.Cases.Count} cases of {contract.Name}";

        public int ChecksPerCase => 4 + contract.DataOut.Count();

        // The registers a program needs for the contract's inputs and outputs.
        public int RegistersNeeded => contract.Data.Count;

        // A register holds a count, so every data port must be one.
        public static bool Fits(Contract contract) => contract.Data.All(port => port.Kind == PortKind.Count);

        public static FunctionProgram Layout(Contract contract, RegisterProgram program) =>
            new FunctionProgram(program, contract.DataIn.Select(port => port.Name).ToList(), contract.DataOut.Select(port => port.Name).ToList(),
                contract.Done.Select(port => port.Name).ToList());

        public FunctionProgram Layout(RegisterProgram program) => Layout(contract, program);

        // Steps enough for a program that moves every unit a few times; scale runs it for longer.
        public static int StepsFor(ContractCase @case, int scale = 1) => scale * (20 * (@case.Inputs.Values.Sum() + @case.Outputs.Values.Sum()) + 100);

        public void NextStage()
        {
        }

        public ScoredProgram Score(RegisterProgram program, int scale = 1)
        {
            budget.Charge(EvaluationKind.InterpreterRun, 1);
            FunctionProgram function = Layout(program);
            if (function.Problem() is string problem)
            {
                return new ScoredProgram(program, -1, problem, new float[ChecksPerCase * contract.Cases.Count]);
            }
            var checks = new List<float>();
            string? firstFailing = null;
            foreach (ContractCase @case in contract.Cases)
            {
                FunctionRun run = function.Run(@case.Inputs, StepsFor(@case, scale), scale * maxConfigurations);
                List<float> own = Checks(run, @case).ToList();
                checks.AddRange(own);
                if (firstFailing == null && own.Any(check => check < 1))
                {
                    firstFailing = $"first failing {@case.Label(contract.DataIn)}: {Reading(run)}";
                }
            }
            return new ScoredProgram(program, checks.Average(), firstFailing ?? "meets every case", checks);
        }

        private IEnumerable<float> Checks(FunctionRun run, ContractCase @case)
        {
            IReadOnlyList<FunctionOutcome> outcomes = run.Outcomes;
            float Over(Func<FunctionOutcome, float> score) => outcomes.Count == 0 ? 0 : outcomes.Average(score);
            yield return Over(outcome => outcome.Done == @case.Done ? 1 : 0);
            foreach (Port port in contract.DataOut)
            {
                yield return Over(outcome => CloseCredit.Score(outcome.Outputs[port.Name], @case.Outputs[port.Name]));
            }
            yield return Over(outcome => outcome.Clean ? 1 : 0);
            yield return Over(outcome => outcome.Latency >= contract.EarliestDone(@case) && outcome.Latency <= contract.MaxLatency ? 1 : 0);
            yield return run.Complete && outcomes.Count > 0 ? 1 : 0;
        }

        private static string Reading(FunctionRun run) =>
            run.Outcomes.Count == 0 ? "no halt"
                : string.Join(" or ", run.Outcomes.Select(outcome => string.Join(",", outcome.Outputs.Select(pair => $"{pair.Key}={pair.Value}").Prepend(outcome.Done)
                    .Append($"latency {outcome.Latency}"))))
                  + (run.Complete ? "" : ", and a computation that did not halt");
    }
}
