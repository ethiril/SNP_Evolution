using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution.Tasks
{
    // Latency counts from the step the start spike reaches the part, so a done neuron fed straight from start has latency 1.
    public sealed class ContractTask : IContractTask, IProposing
    {
        // The start spike is never sent before this step, so a part that fires on its own is caught.
        public const int QuietSteps = 2;

        public static readonly int RuleCount = Enum.GetValues<ContractRule>().Length;

        private static readonly string[] RuleNames = { "quiet before start", "done once", "back to start", "on time" };

        private readonly ContractPorts ports;
        private readonly ContractScoring scoring;

        public ContractTask(Contract contract, PortBinding? binding = null)
        {
            Contract = contract.Validated();
            Binding = (binding ?? PortLayout.AfterInputs(contract)).ValidatedFor(contract);
            Name = "Contract " + contract.Name;
            InputCount = 1 + contract.DataIn.Count();
            // Running on for the maximum latency again shows a second done or a part that is still busy.
            StepsAfterDone = contract.DataOut.Where(port => port.Kind == PortKind.Binary).Select(port => port.Width).DefaultIfEmpty(0).Max() + contract.MaxLatency;
            var watch = new PortWatch(
                PortLayout.OutPorts(contract).Select(port => Binding[port.Name]).ToList(),
                contract.Done.Select(port => Binding[port.Name]).ToList(),
                StepsAfterDone);
            List<EncodedCase> encoded = contract.Cases.Select(@case => PortEncoding.ForCase(contract, @case, QuietSteps)).ToList();
            StartSteps = encoded.Select(@case => @case.StartStep).ToList();
            Cases = encoded.Select(@case => new TaskCase(@case.Input, Readout.Ports, watch)).ToList();
            StepsNeeded = StartSteps.Max() + 1 + contract.MaxLatency + StepsAfterDone + 1;
            ports = new ContractPorts(Contract, StartSteps);
            scoring = new ContractScoring(Contract, ports);
        }

        public Contract Contract { get; }

        public IReadOnlyList<PartPort> Boundary => PortLayout.InputsFirst(Contract, Binding);

        public PortBinding Binding { get; }

        public string Name { get; }

        public int InputCount { get; }

        public IReadOnlyList<TaskCase> Cases { get; }

        public int StepsNeeded { get; }

        public int StepsAfterDone { get; }

        public IReadOnlyList<int> StartSteps { get; }

        public float SolvedFitness => Solved.EveryRun;

        // The task a verifier runs for a task with a contract: the task itself, or one built from its contract.
        public static ContractTask Of(IContractTask task) => task as ContractTask ?? new ContractTask(task.Contract, task.Binding);

        public static int CheckIndex(int caseIndex, ContractRule rule) => caseIndex * RuleCount + (int)rule;

        public static string RuleName(ContractRule rule) => RuleNames[(int)rule];

        public float Score(IReadOnlyList<TrialResult> results) => Checks(results).Average();

        public IReadOnlyList<float> Checks(IReadOnlyList<TrialResult> results) => scoring.Checks(results);

        public string CheckName(int check) => $"{CaseLabel(check / RuleCount)}: {RuleName((ContractRule)(check % RuleCount))}";

        public string Describe(IReadOnlyList<TrialResult> results)
        {
            List<string> failing = Checks(results).Select((score, check) => (score, check)).Where(pair => pair.score < 1)
                .Select(pair => $"{CheckName(pair.check)} {pair.score:0.##}").ToList();
            return failing.Count == 0 ? "meets the contract" : string.Join(Environment.NewLine, failing);
        }

        // Size by slowest latency lets MAP-Elites keep a smaller or quicker part even while it is wrong elsewhere.
        public (int, int)? Niche(IReadOnlyList<TrialResult> results)
        {
            if (results[0].PortRuns.Count == 0)
            {
                return null;
            }
            int neurons = results[0].PortRuns[0].FinalSpikes.Count;
            int slowest = Enumerable.Range(0, Contract.Cases.Count)
                .Select(caseIndex => results[caseIndex].PortRuns.Count == 0 ? null : Latency(results[caseIndex].PortRuns[0], caseIndex))
                .Select(latency => latency is int value && value <= Contract.MaxLatency ? value : Contract.MaxLatency + 1)
                .Max();
            return (neurons, slowest);
        }

        // Null when every case fails, since the proposal would then be the target itself.
        public Contract? Propose(IReadOnlyList<int> unsolvedChecks)
        {
            List<int> failing = unsolvedChecks.Select(check => check / RuleCount).Distinct().Order().ToList();
            if (failing.Count == 0 || failing.Count == Contract.Cases.Count)
            {
                return null;
            }
            return Contract with
            {
                Name = $"{Contract.Name} on {string.Join(" ", failing.Select(CaseLabel))}",
                Cases = failing.Select(caseIndex => Contract.Cases[caseIndex]).ToList(),
            };
        }

        public string Behaviour(IReadOnlyList<TrialResult> results) => BehaviourKey.Of(Contract, ports, results);

        public bool Keeps(PortRun run, int caseIndex, bool timed = true) => scoring.Keeps(run, caseIndex, timed);

        public PortRun? FailingRun(IReadOnlyList<TrialResult> results, int check) => scoring.FailingRun(results, check);

        public string Reading(PortRun run, int caseIndex) => ports.Reading(run, caseIndex);

        public int? Latency(PortRun run, int caseIndex) => ports.Latency(run, caseIndex);

        private string CaseLabel(int caseIndex)
        {
            string label = Contract.Cases[caseIndex].Label(Contract.DataIn);
            return label.Length > 0 ? label : $"case {caseIndex + 1}";
        }
    }

    // Two parts with the same key behave the same on the contract, which is how the library tells parts apart.
    public static class BehaviourKey
    {
        public static string Of(Contract contract, ContractPorts ports, IReadOnlyList<TrialResult> results) =>
            contract.Name + " | " + string.Join(" | ", Enumerable.Range(0, contract.Cases.Count).Select(caseIndex =>
                string.Join(" or ", results[caseIndex].PortRuns.Select(run => ports.Reading(run, caseIndex)).Distinct().OrderBy(reading => reading, StringComparer.Ordinal))));
    }
}
