using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Contracts;

namespace SnpEvolution.Specs.Tasks
{
    public enum ContractRule
    {
        // Nothing is sent on any out-port or done port up to the step the start spike is sent on.
        QuietBeforeStart,

        // Exactly one done fires, the right one, and the data out-ports carry the right values.
        DoneOnce,

        // Every neuron ends with the spikes it began with, so the part can be started again.
        BackToStart,

        // Done fires within the contract's latency.
        OnTime,
    }

    // A wrong but close value earns partial credit, which gives the search a slope.
    public sealed class ContractScoring
    {
        private readonly Contract contract;
        private readonly ContractPorts ports;

        public ContractScoring(Contract contract, ContractPorts ports)
        {
            this.contract = contract;
            this.ports = ports;
        }

        public IReadOnlyList<float> Checks(IReadOnlyList<TrialResult> results) =>
            Enumerable.Range(0, contract.Cases.Count)
                .SelectMany(caseIndex => Enum.GetValues<ContractRule>().Select(rule => OverRuns(rule, results[caseIndex].PortRuns, caseIndex)))
                .ToList();

        // Whether one computation of the case passes every rule; untimed leaves out OnTime, for runs whose timing is meant to vary.
        public bool Keeps(PortRun run, int caseIndex, bool timed = true) =>
            Enum.GetValues<ContractRule>().Where(rule => timed || rule != ContractRule.OnTime).All(rule => Score(rule, run, caseIndex) == 1);

        // The first computation that fails the check, or null when every computation of its case passes it.
        public PortRun? FailingRun(IReadOnlyList<TrialResult> results, int check) =>
            results[check / ContractTask.RuleCount].PortRuns.FirstOrDefault(run => Score((ContractRule)(check % ContractTask.RuleCount), run, check / ContractTask.RuleCount) < 1);

        public float Score(ContractRule rule, PortRun run, int caseIndex) => rule switch
        {
            ContractRule.QuietBeforeStart => run.Firings.Any(firings => firings.Any(firing => firing.Step <= ports.Start(caseIndex))) ? 0 : 1,
            ContractRule.DoneOnce => DoneAndOutputs(run, caseIndex),
            ContractRule.BackToStart => (float)run.FinalSpikes.Where((spikes, neuron) => spikes == run.InitialSpikes[neuron]).Count() / run.FinalSpikes.Count,
            _ => ports.Latency(run, caseIndex) is int latency && latency >= contract.MinLatency && latency <= contract.MaxLatency ? 1 : 0,
        };

        private float OverRuns(ContractRule rule, IReadOnlyList<PortRun> runs, int caseIndex) =>
            runs.Count == 0 ? 0 : runs.Average(run => Score(rule, run, caseIndex));

        private float DoneAndOutputs(PortRun run, int caseIndex)
        {
            List<(string Port, Firing Firing)> dones = ports.Dones(run);
            if (dones.Count != 1 || dones[0].Port != contract.Cases[caseIndex].Done)
            {
                return 0;
            }
            if (ports.DataOut.Count == 0)
            {
                return 1;
            }
            if (contract.OrderedTriggers && !ports.TriggersInOrder(run, caseIndex))
            {
                return 0;
            }
            ContractCase @case = contract.Cases[caseIndex];
            return ports.DataOut.Select((port, slot) => Output(port, ports.AfterStart(run, slot, caseIndex), dones[0].Firing.Step, @case.Outputs[port.Name])).Average();
        }

        private static float Output(Port port, List<Firing> firingsAfterStart, int done, int expected)
        {
            if (ContractPorts.Value(port, firingsAfterStart, done) is not int read)
            {
                return 0;
            }
            if (port.Kind == PortKind.Binary)
            {
                int wrongBits = BitOperations.PopCount((uint)(read ^ expected));
                return wrongBits == 0 ? 1 : CloseCredit.AtBest * (port.Width - wrongBits) / port.Width;
            }
            return port.Kind == PortKind.Trigger ? (read == expected ? 1 : 0) : CloseCredit.Score(read, expected);
        }
    }
}
