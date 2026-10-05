using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution.Tasks
{
    // The network is a part that should meet a contract. Each case feeds the data in-ports (see PortEncoding.ForCase),
    // sends the start spike and watches the out-ports and done ports through the binding. Four rules are checked per
    // case, each scored over every run (every computation, on the exhaustive engine):
    //   1. quiet: nothing is sent on any out-port or done port up to the step the start spike is sent on;
    //   2. done once: exactly one done fires, the right one, and the data out-ports carry the right values;
    //   3. reset: once the run stops, every neuron holds the spikes it began with, so the part can be started again;
    //   4. on time: done fires within the contract's latency.
    // Latency counts from the step the start spike reaches the part (the step after it is sent) to the step done
    // fires, so a done neuron fed straight from the start neuron has latency 1. A run goes on after done for the
    // maximum latency again (plus any binary word), so a second done or a part still busy is seen. Interval,
    // count and trigger out-ports are read strictly between start and done, so a part wired after this one receives
    // them before its own start; a binary out-port's word follows done, bit i on step done + i, as a binary in-port's
    // follows start. A spike on a data out-port outside its window makes that port wrong.
    public sealed class ContractTask : ITask
    {
        // The start spike is never sent before this step, so a part that fires on its own is caught by rule 1.
        public const int QuietSteps = 2;

        public const int RuleCount = 4;

        private const float CloseValueCredit = 0.5f;

        private static readonly string[] RuleNames = { "quiet before start", "done once", "back to start", "on time" };

        private readonly IReadOnlyList<int> startSteps;
        private readonly List<Port> dataOut;
        private readonly List<Port> watched;

        public ContractTask(Contract contract, PortBinding? binding = null)
        {
            Contract = contract.Validated();
            Binding = (binding ?? PortBinding.AfterInputs(contract)).ValidatedFor(contract);
            Name = "Contract " + contract.Name;
            InputCount = 1 + contract.DataIn.Count();
            dataOut = contract.DataOut.ToList();
            watched = PortBinding.OutPorts(contract).ToList();
            int stepsAfterDone = dataOut.Where(port => port.Kind == PortKind.Binary).Select(port => port.Width).DefaultIfEmpty(0).Max() + contract.MaxLatency;
            var watch = new PortWatch(watched.Select(port => Binding[port.Name]).ToList(), contract.Done.Select(port => Binding[port.Name]).ToList(), stepsAfterDone);
            List<EncodedCase> encoded = contract.Cases.Select(@case => PortEncoding.ForCase(contract, @case, QuietSteps)).ToList();
            startSteps = encoded.Select(@case => @case.StartStep).ToList();
            Cases = encoded.Select(@case => new TaskCase(@case.Input, Readout.Ports, watch)).ToList();
            StepsNeeded = startSteps.Max() + 1 + contract.MaxLatency + stepsAfterDone + 1;
        }

        public Contract Contract { get; }

        public PortBinding Binding { get; }

        public string Name { get; }

        public int InputCount { get; }

        public IReadOnlyList<TaskCase> Cases { get; }

        public int StepsNeeded { get; }

        public float Score(IReadOnlyList<TrialResult> results) => Checks(results).Average();

        // Rules 1 to 4 for the first case, then for the second, and so on.
        public IReadOnlyList<float> Checks(IReadOnlyList<TrialResult> results)
        {
            var checks = new List<float>(Contract.Cases.Count * RuleCount);
            for (int index = 0; index < Contract.Cases.Count; index++)
            {
                IReadOnlyList<PortRun> runs = results[index].PortRuns;
                for (int rule = 0; rule < RuleCount; rule++)
                {
                    checks.Add(runs.Count == 0 ? 0 : runs.Average(run => ScoreRule(rule, run, index)));
                }
            }
            return checks;
        }

        public string CheckName(int check) => $"{CaseLabel(check / RuleCount)}: {RuleNames[check % RuleCount]}";

        public string Describe(IReadOnlyList<TrialResult> results)
        {
            IReadOnlyList<float> checks = Checks(results);
            List<string> failing = checks.Select((score, check) => (score, check)).Where(pair => pair.score < 1)
                .Select(pair => $"{CheckName(pair.check)} {pair.score:0.##}").ToList();
            return failing.Count == 0 ? "meets the contract" : string.Join(Environment.NewLine, failing);
        }

        // The network's size by its slowest latency over the cases (one past the maximum when done never fires), so
        // MAP-Elites keeps a smaller or quicker part even while it is wrong elsewhere.
        public (int, int)? Niche(IReadOnlyList<TrialResult> results)
        {
            if (results.Count == 0 || results[0].PortRuns.Count == 0)
            {
                return null;
            }
            int neurons = results[0].PortRuns[0].FinalSpikes.Count;
            int latency = Enumerable.Range(0, Contract.Cases.Count)
                .Select(index => results[index].PortRuns.Count == 0 ? null : Latency(results[index].PortRuns[0], index))
                .Select(latency => latency is int value && value <= Contract.MaxLatency ? value : Contract.MaxLatency + 1)
                .Max();
            return (neurons, latency);
        }

        private string CaseLabel(int index)
        {
            string label = Contract.Cases[index].Label(Contract.DataIn);
            return label.Length > 0 ? label : $"case {index + 1}";
        }

        private float ScoreRule(int rule, PortRun run, int index)
        {
            int start = startSteps[index];
            switch (rule)
            {
                case 0:
                    return run.Firings.Any(firings => firings.Any(firing => firing.Step <= start)) ? 0 : 1;
                case 1:
                    return ScoreDoneAndOutputs(run, index);
                case 2:
                    return (float)run.FinalSpikes.Where((spikes, neuron) => spikes == run.InitialSpikes[neuron]).Count() / run.FinalSpikes.Count;
                default:
                    int? latency = Latency(run, index);
                    return latency >= Contract.MinLatency && latency <= Contract.MaxLatency ? 1 : 0;
            }
        }

        private float ScoreDoneAndOutputs(PortRun run, int index)
        {
            ContractCase @case = Contract.Cases[index];
            List<(string Port, Firing Firing)> dones = Contract.Done
                .SelectMany((port, slot) => run.Firings[dataOut.Count + slot].Select(firing => (port.Name, firing)))
                .ToList();
            if (dones.Count != 1 || dones[0].Port != @case.Done)
            {
                return 0;
            }
            int start = startSteps[index];
            int done = dones[0].Firing.Step;
            return dataOut.Count == 0
                ? 1
                : dataOut.Select((port, slot) => ScoreValue(port, run.Firings[slot].Where(firing => firing.Step > start).ToList(), start, done, @case.Outputs[port.Name])).Average();
        }

        // Firings are the port's firings after start.
        private static float ScoreValue(Port port, List<Firing> firings, int start, int done, int expected)
        {
            if (port.Kind == PortKind.Binary)
            {
                if (firings.Any(firing => firing.Step < done || firing.Step >= done + port.Width))
                {
                    return 0;
                }
                int word = firings.Aggregate(0, (bits, firing) => bits | 1 << (firing.Step - done));
                int wrongBits = System.Numerics.BitOperations.PopCount((uint)(word ^ expected));
                return wrongBits == 0 ? 1 : CloseValueCredit * (port.Width - wrongBits) / port.Width;
            }
            if (firings.Any(firing => firing.Step >= done))
            {
                return 0;
            }
            int? value = port.Kind switch
            {
                PortKind.Count => (int)firings.Sum(firing => firing.Spikes),
                PortKind.Interval => firings.Count == 2 ? firings[1].Step - firings[0].Step : null,
                _ => firings.Count <= 1 ? firings.Count : null,
            };
            if (value == expected)
            {
                return 1;
            }
            return value is int close && port.Kind != PortKind.Trigger ? CloseValueCredit / (1 + Math.Abs(close - expected)) : 0;
        }

        private int? Latency(PortRun run, int index) => FirstDone(run)?.Step - (startSteps[index] + 1);

        private Firing? FirstDone(PortRun run) =>
            run.Firings.Skip(dataOut.Count).SelectMany(firings => firings).OrderBy(firing => firing.Step).Cast<Firing?>().FirstOrDefault();
    }
}
