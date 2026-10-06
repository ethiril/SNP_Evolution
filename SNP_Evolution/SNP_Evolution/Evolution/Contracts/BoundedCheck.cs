using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Export;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution.Contracts
{
    // UpTo is the largest bound N for which every input with every value at most N meets the contract, -1 when not even
    // N = 0 is proven. AllInputs says the bound covers every input there is, as it does for a part with no data in-ports
    // or only binary ones. Stopped says why the check went no further; FailsAt names the input of a counterexample.
    public sealed record ProvenBound(int UpTo, bool AllInputs, string Stopped, [property: JsonProperty(NullValueHandling = NullValueHandling.Ignore)] string? FailsAt = null)
    {
        public override string ToString() => AllInputs ? "proven for every input" : UpTo < 0 ? $"not proven ({Stopped})" : $"proven up to {UpTo} ({Stopped})";
    }

    // An input the part fails on, the check it fails, what the contract expects and what the failing computation read,
    // and the steps of that computation.
    public sealed record Counterexample(string Inputs, string Check, string Expected, string Read, string Trace)
    {
        public override string ToString() => $"Counterexample at {Inputs}: {Check} fails. Expected {Expected}; read {Read}.\n{Trace}";
    }

    public sealed record BoundedResult(ProvenBound Proven, Counterexample? Counterexample)
    {
        public bool Refuted => Counterexample != null;
    }

    // MaxBound stops the check at a bound even with time to spare. A bound started before the time runs out is finished.
    public sealed record ProofLimits(TimeSpan Time, int MaxBound = int.MaxValue, int MaxConfigurations = PartEvolution.VerifyConfigurations);

    // Proves a part meets its contract for every input up to a bound N, raising N until the time runs out. Each bound
    // runs the inputs whose largest value is N, on the exhaustive engine, with the latency the specification allows at N;
    // a case too wide to follow exactly is not proven, since sampling is not proof.
    public static class BoundedCheck
    {
        // A part entering the library is checked up to twice the largest value its cases test, where a composition built
        // from it is likely to call it, or for as long as this allows.
        public static readonly TimeSpan AdmissionTime = TimeSpan.FromSeconds(10);

        private const int Repetitions = 20;

        public static BoundedResult Prove(Part part, ProofLimits limits)
        {
            Contract contract = part.Contract;
            if (Specifications.For(contract) is not Specification specification)
            {
                return new BoundedResult(new ProvenBound(-1, false, "the contract has no specification"), null);
            }
            if (Specifications.Disagreements(contract, specification).FirstOrDefault() is string disagreement)
            {
                return new BoundedResult(new ProvenBound(-1, false, $"the contract disagrees with its specification: {disagreement}"), null);
            }
            var engine = new ExhaustiveCpuEngine(limits.MaxConfigurations);
            var clock = Stopwatch.StartNew();
            int proven = -1;
            for (int bound = 0; bound <= limits.MaxBound; bound++)
            {
                if (clock.Elapsed > limits.Time)
                {
                    return Stop(proven, $"time limit of {limits.Time.TotalSeconds:0.#} s");
                }
                List<IReadOnlyDictionary<string, int>> inputs = InputsWithLargest(contract, bound).Where(specification.InDomain).ToList();
                if (inputs.Count > 0)
                {
                    Contract atBound = contract with
                    {
                        Cases = inputs.Select(specification.Expected).ToList(),
                        MaxLatency = Math.Max(contract.MaxLatency, specification.Latency(bound)),
                    };
                    var task = new ContractTask(atBound, part.Binding);
                    var options = new SimulationOptions(task.StepsNeeded, Repetitions, OutputTiming.Interval);
                    IReadOnlyList<TrialResult> results = engine.Run(task.Cases.Select(@case => new Trial(part.Network, @case.Input, @case.Readout, @case.Watch)).ToList(), options, new Random(0));
                    if (results.Select((result, index) => (result, index)).FirstOrDefault(pair => !pair.result.Exact) is { result: not null } inexact)
                    {
                        return Stop(proven, $"{Label(atBound, inexact.index)} has too many computations to follow exactly");
                    }
                    int failing = task.Checks(results).Select((score, check) => (score, check)).Where(pair => pair.score < 1).Select(pair => pair.check).DefaultIfEmpty(-1).First();
                    if (failing >= 0)
                    {
                        string input = Label(atBound, failing / ContractTask.RuleCount);
                        return new BoundedResult(new ProvenBound(proven, false, $"counterexample at {input}", input), CounterexampleOf(part, task, results, failing));
                    }
                }
                proven = bound;
                if (Exhausted(contract, bound))
                {
                    return new BoundedResult(new ProvenBound(proven, true, "every input checked"), null);
                }
            }
            return Stop(proven, $"bound {limits.MaxBound} reached");
        }

        // Admission limits: twice the largest value the contract's cases test, in at most AdmissionTime.
        public static ProofLimits Admission(Contract contract) =>
            new ProofLimits(AdmissionTime, 2 * contract.Cases.SelectMany(@case => @case.Inputs.Values).DefaultIfEmpty(0).Max());

        // Null, with the counterexample logged, when the part fails its contract within the admission limits.
        public static ProvenBound? Admit(Part part, Action<string> log)
        {
            BoundedResult result = Prove(part, Admission(part.Contract));
            if (result.Counterexample is Counterexample counterexample)
            {
                log($"Not admitted: the part for {part.Contract.Name} passes its test cases but not every input. {counterexample}");
                return null;
            }
            return result.Proven;
        }

        // Every input whose largest value is exactly the bound, each value within its port's range.
        public static IEnumerable<IReadOnlyDictionary<string, int>> InputsWithLargest(Contract contract, int bound)
        {
            List<Port> ports = contract.DataIn.ToList();
            if (ports.Count == 0)
            {
                return bound == 0 ? new[] { new Dictionary<string, int>() } : Array.Empty<IReadOnlyDictionary<string, int>>();
            }
            IEnumerable<int[]> values = new[] { Array.Empty<int>() };
            foreach (Port port in ports)
            {
                IEnumerable<int> range = Enumerable.Range(Smallest(port), Math.Max(0, Math.Min(bound, Largest(port)) - Smallest(port) + 1));
                values = values.SelectMany(prefix => range.Select(value => prefix.Append(value).ToArray())).ToList();
            }
            return values.Where(row => row.Max() == bound).Select(row => (IReadOnlyDictionary<string, int>)ports.Zip(row).ToDictionary(pair => pair.First.Name, pair => pair.Second));
        }

        private static bool Exhausted(Contract contract, int bound) => contract.DataIn.All(port => port.Kind == PortKind.Binary && Largest(port) <= bound);

        private static int Smallest(Port port) => port.Kind == PortKind.Interval ? 1 : 0;

        private static int Largest(Port port) => port.Kind == PortKind.Binary ? (1 << port.Width) - 1 : int.MaxValue;

        private static BoundedResult Stop(int proven, string reason) => new BoundedResult(new ProvenBound(proven, false, reason), null);

        private static string Label(Contract contract, int caseIndex) =>
            contract.Cases[caseIndex].Label(contract.DataIn) is { Length: > 0 } label ? label : "the one case";

        private static Counterexample CounterexampleOf(Part part, ContractTask task, IReadOnlyList<TrialResult> results, int check)
        {
            int caseIndex = check / ContractTask.RuleCount;
            ContractCase @case = task.Contract.Cases[caseIndex];
            PortRun run = task.FailingRun(results, check)!;
            string expected = string.Join(",", new[] { @case.Done }.Concat(@case.Outputs.Select(pair => $"{pair.Key}={pair.Value}")));
            string name = task.CheckName(check);
            string rule = name[(name.IndexOf(": ", StringComparison.Ordinal) + 2)..];
            string trace = SpikeTrace.Choices(part.Network).Count == 0
                ? Table(part, SpikeTrace.Run(part.Network, task.Cases[caseIndex].Input, task.StepsNeeded))
                : Firings(part, run);
            return new Counterexample(Label(task.Contract, caseIndex), rule, expected, task.Reading(run, caseIndex), trace);
        }

        // One row per step up to the last that does anything, one column per neuron named by its port: the spikes it holds
        // after the step, marked * when it fired and ~ when it forgot.
        private static string Table(Part part, SpikeTrace trace)
        {
            string[] names = Names(part);
            int last = Enumerable.Range(0, trace.Steps.Count).LastOrDefault(step => trace.Steps[step].Any(row => row.Applied || row.Held != row.After));
            var cells = new List<string[]> { new[] { "step" }.Concat(names).ToArray() };
            cells.AddRange(trace.Steps.Take(last + 2).Select((rows, step) => new[] { step.ToString(CultureInfo.InvariantCulture) }
                .Concat(rows.Select(row => row.After.ToString(CultureInfo.InvariantCulture) + (row.Sent > 0 ? "*" : row.Applied ? "~" : ""))).ToArray()));
            int[] widths = Enumerable.Range(0, cells[0].Length).Select(column => cells.Max(row => row[column].Length)).ToArray();
            var text = new StringBuilder("Spikes held after each step (* fired, ~ forgot):\n");
            foreach (string[] row in cells)
            {
                text.AppendLine(string.Join("  ", row.Select((cell, column) => cell.PadLeft(widths[column]))));
            }
            return text.ToString().TrimEnd();
        }

        // The network is not deterministic, so only the failing computation's port firings are known.
        private static string Firings(Part part, PortRun run)
        {
            List<Port> watched = PortBinding.OutPorts(part.Contract).ToList();
            return "Port firings of the failing computation (the network is not deterministic, so no neuron trace):\n" +
                string.Join("\n", watched.Select((port, slot) =>
                    $"{port.Name}: " + (run.Firings[slot].Count == 0 ? "never" : "steps " + string.Join(", ", run.Firings[slot].Select(firing => firing.Spikes > 1 ? $"{firing.Step} (x{firing.Spikes})" : $"{firing.Step}")))));
        }

        // Port names where a neuron is a port, #k for the rest.
        private static string[] Names(Part part)
        {
            var names = Enumerable.Range(1, part.Network.Neurons.Count).Select(position => $"#{position}").ToArray();
            foreach (PartPort port in part.Ports())
            {
                names[port.Position - 1] = port.Port.Name;
            }
            return names;
        }
    }
}
