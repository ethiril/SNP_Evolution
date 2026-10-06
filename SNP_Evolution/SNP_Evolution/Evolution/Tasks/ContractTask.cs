using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution.Tasks
{
    // What each case of a ContractTask is checked for, scored over every run.
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

    // Latency counts from the step the start spike reaches the part, so a done neuron fed straight from the start neuron has latency 1.
    // Interval, count and trigger out-ports are read strictly between start and done, and a binary word follows done, bit i on step done + i.
    public sealed class ContractTask : ITask
    {
        // The start spike is never sent before this step, so a part that fires on its own is caught.
        public const int QuietSteps = 2;

        public static readonly int RuleCount = Enum.GetValues<ContractRule>().Length;

        private static readonly string[] RuleNames = { "quiet before start", "done once", "back to start", "on time" };

        private readonly IReadOnlyList<int> startSteps;
        private readonly List<Port> dataOut;

        public ContractTask(Contract contract, PortBinding? binding = null)
        {
            Contract = contract.Validated();
            Binding = (binding ?? PortLayout.AfterInputs(contract)).ValidatedFor(contract);
            Name = "Contract " + contract.Name;
            InputCount = 1 + contract.DataIn.Count();
            dataOut = contract.DataOut.ToList();
            // Running on for the maximum latency again shows a second done or a part that is still busy.
            StepsAfterDone = dataOut.Where(port => port.Kind == PortKind.Binary).Select(port => port.Width).DefaultIfEmpty(0).Max() + contract.MaxLatency;
            var watch = new PortWatch(
                PortLayout.OutPorts(contract).Select(port => Binding[port.Name]).ToList(),
                contract.Done.Select(port => Binding[port.Name]).ToList(),
                StepsAfterDone);
            List<EncodedCase> encoded = contract.Cases.Select(@case => PortEncoding.ForCase(contract, @case, QuietSteps)).ToList();
            startSteps = encoded.Select(@case => @case.StartStep).ToList();
            Cases = encoded.Select(@case => new TaskCase(@case.Input, Readout.Ports, watch)).ToList();
            StepsNeeded = startSteps.Max() + 1 + contract.MaxLatency + StepsAfterDone + 1;
        }

        public Contract Contract { get; }

        // The contract's ports where a network for it has them, so composition search can wire parts to them by type.
        public IReadOnlyList<PartPort> Boundary => PortLayout.InputsFirst(Contract, Binding);

        public PortBinding Binding { get; }

        public string Name { get; }

        public int InputCount { get; }

        public IReadOnlyList<TaskCase> Cases { get; }

        public int StepsNeeded { get; }

        public int StepsAfterDone { get; }

        // The step each case sends its start spike on.
        public IReadOnlyList<int> StartSteps => startSteps;

        public float SolvedFitness => Solved.EveryRun;

        public static int CheckIndex(int caseIndex, ContractRule rule) => caseIndex * RuleCount + (int)rule;

        public float Score(IReadOnlyList<TrialResult> results) => Checks(results).Average();

        public IReadOnlyList<float> Checks(IReadOnlyList<TrialResult> results) =>
            Enumerable.Range(0, Contract.Cases.Count)
                .SelectMany(caseIndex => Enum.GetValues<ContractRule>().Select(rule => ScoreRuleOverRuns(rule, results[caseIndex].PortRuns, caseIndex)))
                .ToList();

        // What every case read, as text: per distinct computation the done ports that fired and each data out-port's value
        // ("?" when it is no value), with the contract's name. Two parts with the same text behave the same on the contract.
        public string Behaviour(IReadOnlyList<TrialResult> results) =>
            Contract.Name + " | " + string.Join(" | ", Enumerable.Range(0, Contract.Cases.Count).Select(caseIndex =>
                string.Join(" or ", results[caseIndex].PortRuns.Select(run => Reading(run, caseIndex)).Distinct().OrderBy(reading => reading, StringComparer.Ordinal))));

        public string CheckName(int check) => $"{CaseLabel(check / RuleCount)}: {RuleName((ContractRule)(check % RuleCount))}";

        public static string RuleName(ContractRule rule) => RuleNames[(int)rule];

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

        // Whether one computation of the case passes every rule; untimed leaves out OnTime, for runs whose timing is meant to vary.
        public bool Keeps(PortRun run, int caseIndex, bool timed = true) =>
            Enum.GetValues<ContractRule>().Where(rule => timed || rule != ContractRule.OnTime).All(rule => ScoreRule(rule, run, caseIndex) == 1);

        // The first computation that fails the check, or null when every computation of its case passes it.
        public PortRun? FailingRun(IReadOnlyList<TrialResult> results, int check) =>
            results[check / RuleCount].PortRuns.FirstOrDefault(run => ScoreRule((ContractRule)(check % RuleCount), run, check / RuleCount) < 1);

        private string CaseLabel(int caseIndex)
        {
            string label = Contract.Cases[caseIndex].Label(Contract.DataIn);
            return label.Length > 0 ? label : $"case {caseIndex + 1}";
        }

        private float ScoreRuleOverRuns(ContractRule rule, IReadOnlyList<PortRun> runs, int caseIndex) =>
            runs.Count == 0 ? 0 : runs.Average(run => ScoreRule(rule, run, caseIndex));

        private float ScoreRule(ContractRule rule, PortRun run, int caseIndex) => rule switch
        {
            ContractRule.QuietBeforeStart => run.Firings.Any(firings => firings.Any(firing => firing.Step <= startSteps[caseIndex])) ? 0 : 1,
            ContractRule.DoneOnce => ScoreDoneAndOutputs(run, caseIndex),
            ContractRule.BackToStart => (float)run.FinalSpikes.Where((spikes, neuron) => spikes == run.InitialSpikes[neuron]).Count() / run.FinalSpikes.Count,
            _ => Latency(run, caseIndex) is int latency && latency >= Contract.MinLatency && latency <= Contract.MaxLatency ? 1 : 0,
        };

        private float ScoreDoneAndOutputs(PortRun run, int caseIndex)
        {
            ContractCase @case = Contract.Cases[caseIndex];
            List<(string Port, Firing Firing)> dones = Contract.Done
                .SelectMany((port, slot) => run.Firings[dataOut.Count + slot].Select(firing => (port.Name, firing)))
                .ToList();
            if (dones.Count != 1 || dones[0].Port != @case.Done)
            {
                return 0;
            }
            if (dataOut.Count == 0)
            {
                return 1;
            }
            if (Contract.OrderedTriggers && !TriggersInOrder(run, startSteps[caseIndex]))
            {
                return 0;
            }
            return ScoreOutputs(run, caseIndex, dones[0].Firing.Step);
        }

        private float ScoreOutputs(PortRun run, int caseIndex, int done)
        {
            ContractCase @case = Contract.Cases[caseIndex];
            int start = startSteps[caseIndex];
            return dataOut.Select((port, slot) =>
            {
                List<Firing> firingsAfterStart = run.Firings[slot].Where(firing => firing.Step > start).ToList();
                int expected = @case.Outputs[port.Name];
                return port.Kind == PortKind.Binary ? ScoreBinaryWord(port.Width, firingsAfterStart, done, expected) : ScoreUnaryValue(port.Kind, firingsAfterStart, done, expected);
            }).Average();
        }

        // What one computation of the case read: the done ports that fired, then each data out-port's value ("?" when it is no value).
        public string Reading(PortRun run, int caseIndex)
        {
            int start = startSteps[caseIndex];
            List<(string Port, Firing Firing)> dones = Contract.Done
                .SelectMany((port, slot) => run.Firings[dataOut.Count + slot].Select(firing => (Port: port.Name, Firing: firing)))
                .OrderBy(done => done.Firing.Step)
                .ToList();
            int done = dones.Count == 0 ? int.MaxValue : dones[0].Firing.Step;
            IEnumerable<string> values = dataOut.Select((port, slot) =>
            {
                List<Firing> firingsAfterStart = run.Firings[slot].Where(firing => firing.Step > start).ToList();
                int? value = port.Kind == PortKind.Binary ? ReadBinaryWord(port.Width, firingsAfterStart, done) : ReadUnaryValue(port.Kind, firingsAfterStart, done);
                return $"{port.Name}={value?.ToString() ?? "?"}";
            });
            return string.Join(",", dones.Select(done => done.Port).DefaultIfEmpty("no done").Concat(values));
        }

        // Each trigger out-port's first firing after start comes on a later step than the one listed before it.
        private bool TriggersInOrder(PortRun run, int start)
        {
            List<int> firstSteps = dataOut.Select((port, slot) => (port, slot))
                .Where(pair => pair.port.Kind == PortKind.Trigger)
                .Select(pair => run.Firings[pair.slot].Where(firing => firing.Step > start).Select(firing => firing.Step).DefaultIfEmpty(-1).First())
                .Where(step => step >= 0)
                .ToList();
            return firstSteps.Zip(firstSteps.Skip(1)).All(pair => pair.First < pair.Second);
        }

        private static float ScoreBinaryWord(int width, List<Firing> firingsAfterStart, int done, int expected)
        {
            if (ReadBinaryWord(width, firingsAfterStart, done) is not int word)
            {
                return 0;
            }
            int wrongBits = BitOperations.PopCount((uint)(word ^ expected));
            return wrongBits == 0 ? 1 : CloseCredit.AtBest * (width - wrongBits) / width;
        }

        private static float ScoreUnaryValue(PortKind kind, List<Firing> firingsAfterStart, int done, int expected)
        {
            if (ReadUnaryValue(kind, firingsAfterStart, done) is not int read)
            {
                return 0;
            }
            return kind == PortKind.Trigger ? (read == expected ? 1 : 0) : CloseCredit.Score(read, expected);
        }

        // Null when the word spills outside its window after done.
        private static int? ReadBinaryWord(int width, List<Firing> firingsAfterStart, int done) =>
            firingsAfterStart.Any(firing => firing.Step < done || firing.Step >= done + width)
                ? null
                : firingsAfterStart.Aggregate(0, (bits, firing) => bits | 1 << (firing.Step - done));

        // Null when the port fires at or after done, or its firings are no value of its kind.
        private static int? ReadUnaryValue(PortKind kind, List<Firing> firingsAfterStart, int done)
        {
            if (firingsAfterStart.Any(firing => firing.Step >= done))
            {
                return null;
            }
            return kind switch
            {
                PortKind.Count => (int)firingsAfterStart.Sum(firing => firing.Spikes),
                PortKind.Interval => firingsAfterStart.Count == 2 ? firingsAfterStart[1].Step - firingsAfterStart[0].Step : null,
                _ => firingsAfterStart.Count <= 1 ? firingsAfterStart.Count : null,
            };
        }

        // The step the computation's first done fired on, counted from the step start reaches the part; null when none fired.
        public int? Latency(PortRun run, int caseIndex) => FirstDone(run)?.Step - (startSteps[caseIndex] + 1);

        private Firing? FirstDone(PortRun run) =>
            run.Firings.Skip(dataOut.Count).SelectMany(firings => firings).OrderBy(firing => firing.Step).Cast<Firing?>().FirstOrDefault();
    }
}
