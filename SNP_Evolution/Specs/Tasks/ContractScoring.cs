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

        // Exactly one done fires, and it is the right one.
        DoneOnce,

        // The data out-ports carry the right values, read up to the right done, or the first done when it did not fire, or
        // to the end when none did, so a part that computes but misfires done still scores. Always met without data out-ports.
        Values,

        // Every neuron ends with the spikes it began with, so the part can be started again.
        BackToStart,

        // Done fires within the contract's latency, and not before the last trigger in-port has fired.
        OnTime,
    }

    // A wrong but close value earns partial credit, which gives the search a slope. A part with data out-ports scores
    // its values as half of each case, since a part that ignores its input meets every other rule (it only has to fire
    // done on time and keep quiet), and with equal weights scored 0.8 while a part with the right values and one rule
    // wrong scored less.
    public sealed class ContractScoring
    {
        public const float ValuesWeight = 0.5f;

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

        // The mean over cases; with data out-ports each case is half its values and half its other rules.
        public float Fitness(IReadOnlyList<float> checks)
        {
            int rules = Enum.GetValues<ContractRule>().Length;
            return Enumerable.Range(0, checks.Count / rules).Select(caseIndex =>
            {
                List<float> own = checks.Skip(caseIndex * rules).Take(rules).ToList();
                float others = own.Where((_, rule) => rule != (int)ContractRule.Values).Average();
                return ports.DataOut.Count == 0 ? others : ValuesWeight * own[(int)ContractRule.Values] + (1 - ValuesWeight) * others;
            }).Average();
        }

        // Whether one computation of the case passes every rule; untimed leaves out OnTime, for runs whose timing is meant to vary.
        public bool Keeps(PortRun run, int caseIndex, bool timed = true) =>
            Enum.GetValues<ContractRule>().Where(rule => timed || rule != ContractRule.OnTime).All(rule => Score(rule, run, caseIndex) == 1);

        // The first computation that fails the check, or null when every computation of its case passes it.
        public PortRun? FailingRun(IReadOnlyList<TrialResult> results, int check) =>
            results[check / ContractTask.RuleCount].PortRuns.FirstOrDefault(run => Score((ContractRule)(check % ContractTask.RuleCount), run, check / ContractTask.RuleCount) < 1);

        public float Score(ContractRule rule, PortRun run, int caseIndex) => rule switch
        {
            ContractRule.QuietBeforeStart => run.Firings.Any(firings => firings.Any(firing => firing.Step <= ports.Start(caseIndex))) ? 0 : 1,
            ContractRule.DoneOnce => ports.Dones(run) is { Count: 1 } dones && dones[0].Port == contract.Cases[caseIndex].Done ? 1 : 0,
            ContractRule.Values => Values(run, caseIndex),
            ContractRule.BackToStart => (float)run.FinalSpikes.Where((spikes, neuron) => spikes == run.InitialSpikes[neuron]).Count() / run.FinalSpikes.Count,
            _ => ports.Latency(run, caseIndex) is int latency && latency >= contract.EarliestDone(contract.Cases[caseIndex]) && latency <= contract.MaxLatency ? 1 : 0,
        };

        private float OverRuns(ContractRule rule, IReadOnlyList<PortRun> runs, int caseIndex) =>
            runs.Count == 0 ? 0 : runs.Average(run => Score(rule, run, caseIndex));

        private float Values(PortRun run, int caseIndex)
        {
            if (ports.DataOut.Count == 0)
            {
                return 1;
            }
            if ((contract.OrderedTriggers && !ports.TriggersInOrder(run, caseIndex)) || (contract.TogetherTriggers && !ports.TriggersTogether(run, caseIndex)))
            {
                return 0;
            }
            List<(string Port, Firing Firing)> dones = ports.Dones(run).OrderBy(done => done.Firing.Step).ToList();
            ContractCase @case = contract.Cases[caseIndex];
            int done = dones.Where(fired => fired.Port == @case.Done).Concat(dones).Select(fired => fired.Firing.Step).DefaultIfEmpty(int.MaxValue).First();
            return ports.DataOut.Select((port, slot) => Output(port, ports.AfterStart(run, slot, caseIndex), done, @case.Outputs[port.Name])).Average();
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
