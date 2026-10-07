using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Specs.Verification
{
    // Proven is how far the check got and why it stopped; Verdict passes when every input was checked, fails on a
    // counterexample, and is unknown when the check stopped before either.
    public sealed record BoundedResult(ProvenBound Proven, Verdict Verdict);

    // MaxBound stops the check at a bound even with time to spare. A bound started before the time runs out is finished.
    public sealed record ProofLimits(TimeSpan Time, int MaxBound = int.MaxValue, int MaxConfigurations = Verifier.MaxConfigurations);

    // Checks a part on every input up to growing bounds, each bound through the Verifier. A case too wide for the
    // exhaustive engine to follow exactly stops the proof, since sampling is not proof. Each bound checked is charged as a
    // proof step, and its run on the exhaustive engine as a check.
    public static class BoundedCheck
    {
        // A part entering the library is checked up to twice the largest value its cases test, where a composition built
        // from it is likely to call it, or for as long as this allows.
        public static readonly TimeSpan AdmissionTime = TimeSpan.FromSeconds(10);

        public static BoundedResult Prove(Part part, ProofLimits limits, EvaluationBudget budget)
        {
            Contract contract = part.Contract;
            if (Specification.For(contract) is not Specification specification)
            {
                return Stopped(-1, new StopReason(Stop.NoSpecification));
            }
            var clock = Stopwatch.StartNew();
            int proven = -1;
            for (int bound = 0; bound <= limits.MaxBound; bound++)
            {
                if (clock.Elapsed > limits.Time)
                {
                    return Stopped(proven, new StopReason(Stop.TimeLimit, $"{limits.Time.TotalSeconds:0.#}"));
                }
                if (EarlyStop.Requested)
                {
                    return Stopped(proven, new StopReason(Stop.StoppedEarly));
                }
                if (AtBound(contract, specification, bound) is Contract atBound)
                {
                    budget.Charge(EvaluationKind.ProofStep, 1);
                    switch (new Verifier(new ContractTask(atBound, part.Binding), budget, limits.MaxConfigurations).Check(part.Network))
                    {
                        case Verdict.Unknown unknown:
                            return Stopped(proven, unknown.Reason);
                        case Verdict.Failed failed:
                            string input = failed.Counterexample.Inputs;
                            return new BoundedResult(new ProvenBound(proven, false, new StopReason(Stop.Counterexample, input), input), failed);
                    }
                }
                proven = bound;
                if (BoundedInputs.Exhausted(contract, bound) || bound >= specification.LargestInput)
                {
                    return new BoundedResult(new ProvenBound(proven, true, new StopReason(Stop.EveryInputChecked)), new Verdict.Passed());
                }
            }
            return Stopped(proven, new StopReason(Stop.BoundReached, $"{limits.MaxBound}"));
        }

        // The contract for every input whose largest value is the bound, with the latency the specification allows there;
        // null when no such input is in the specification's domain, or the contract has no specification.
        public static Contract? AtBound(Contract contract, int bound) =>
            Specification.For(contract) is Specification specification ? AtBound(contract, specification, bound) : null;

        private static Contract? AtBound(Contract contract, Specification specification, int bound)
        {
            List<IReadOnlyDictionary<string, int>> inputs = BoundedInputs.WithLargest(contract, bound).Where(specification.InDomain).ToList();
            return inputs.Count == 0 ? null : contract with
            {
                Cases = inputs.Select(specification.Expected).ToList(),
                MaxLatency = Math.Max(contract.MaxLatency, inputs.Max(specification.Latency)),
            };
        }

        public static ProofLimits Admission(Contract contract) =>
            new ProofLimits(AdmissionTime, 2 * contract.Cases.SelectMany(@case => @case.Inputs.Values).DefaultIfEmpty(0).Max());

        // The bounded check a part entering the library gets; a counterexample keeps it out, and is logged.
        public static BoundedResult Admit(Part part, EvaluationBudget budget, Action<string> log)
        {
            BoundedResult result = Prove(part, Admission(part.Contract), budget);
            if (result.Verdict is Verdict.Failed failed)
            {
                log($"Not admitted: the part for {part.Contract.Name} passes its test cases but not every input. {CounterexampleText.Of(part, failed.Counterexample)}");
            }
            return result;
        }

        private static BoundedResult Stopped(int proven, StopReason reason) => new BoundedResult(new ProvenBound(proven, false, reason), new Verdict.Unknown(reason));
    }
}
