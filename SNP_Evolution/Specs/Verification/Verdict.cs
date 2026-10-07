using SnpEvolution.Simulation;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Specs.Verification
{
    // What checking a network against a contract found: it passed, it failed on a counterexample, or nothing could be
    // said, and why.
    public abstract record Verdict
    {
        private Verdict()
        {
        }

        public sealed record Passed : Verdict;

        public sealed record Failed(Counterexample Counterexample) : Verdict;

        public sealed record Unknown(StopReason Reason) : Verdict;
    }

    // An input a network fails: the contract it was checked on, the case, which holds the inputs and the outputs and done
    // the contract expects, the rule it breaks, what the failing computation read, the step that computation's first done
    // fired on counted from the step start reaches the part (null when none did), and the computation itself. A search
    // can add Case to the cases it scores on.
    public sealed record Counterexample(Contract Contract, ContractCase Case, ContractRule Rule, string Read, int? DoneStep, PortRun Run)
    {
        public string Inputs => InputsOf(Contract, Case);

        // The case's inputs as "n=3", or "the one case" for a part with no data in-ports.
        public static string InputsOf(Contract contract, ContractCase @case) => @case.Label(contract.DataIn) is { Length: > 0 } label ? label : "the one case";
    }
}
