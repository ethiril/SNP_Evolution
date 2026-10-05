using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SnpEvolution.Evolution.Contracts
{
    // How a value travels along a port, as spikes on the port's neuron.
    [JsonConverter(typeof(StringEnumConverter))]
    public enum PortKind
    {
        // Two spikes n steps apart, as SN P systems usually encode a number. n is at least 1.
        Interval,

        // n spikes in all; 0 is no spikes.
        Count,

        // One spike, meaning start, done or a branch taken. As a data port its value is 1 when it fires and 0 when not.
        Trigger,

        // Bit i of n, least significant first, is a spike or silence on the i-th step of the word, for i below the
        // port's width. Interval and count are unary, so a k-bit number costs up to 2^k steps; binary costs k.
        Binary,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum PortDirection
    {
        In,
        Out,
    }

    // A named way into or out of a part. Width is the number of bits of a Binary port, and 0 for every other kind.
    public sealed record Port(string Name, PortDirection Direction, PortKind Kind, int Width = 0)
    {
        public const int MaxBinaryWidth = 30;

        public static Port In(string name, PortKind kind, int width = 0) => new Port(name, PortDirection.In, kind, width);

        public static Port Out(string name, PortKind kind, int width = 0) => new Port(name, PortDirection.Out, kind, width);

        public override string ToString() => $"{Name} ({Direction} {Kind}{(Kind == PortKind.Binary ? " " + Width : "")})";
    }

    // One test of a part: the value fed to each data in-port, the value expected on each data out-port, and the done
    // port that should fire, which for a part with branches (a zero test) says which branch is right.
    public sealed record ContractCase(IReadOnlyDictionary<string, int> Inputs, IReadOnlyDictionary<string, int> Outputs, string Done)
    {
        // Inputs as "n=3" or "a=1,b=2", in the given port order; empty when the part has no data in-ports.
        public string Label(IEnumerable<Port> inPorts) =>
            string.Join(",", inPorts.Where(port => Inputs.ContainsKey(port.Name)).Select(port => $"{port.Name}={Inputs[port.Name]}"));
    }

    // What a part does, independent of any network: it waits for a spike on Start, reads its data in-ports, writes its
    // data out-ports and fires exactly one done port within MaxLatency steps, then is back where it started. Which
    // neurons carry which ports is the part's business, not the contract's. MinLatency lets a timer demand that done
    // fires no sooner than a given step, such as a delay that must take exactly k steps.
    public sealed record Contract(
        string Name,
        Port Start,
        IReadOnlyList<Port> Done,
        IReadOnlyList<Port> Data,
        IReadOnlyList<ContractCase> Cases,
        int MaxLatency,
        int MinLatency = 0)
    {
        [JsonIgnore]
        public IEnumerable<Port> DataIn => Data.Where(port => port.Direction == PortDirection.In);

        [JsonIgnore]
        public IEnumerable<Port> DataOut => Data.Where(port => port.Direction == PortDirection.Out);

        public static Contract FromJson(string json) =>
            JsonConvert.DeserializeObject<Contract>(json) ?? throw new FormatException("The JSON holds no contract.");

        public string ToJson() => JsonConvert.SerializeObject(this, Formatting.Indented);

        // Everything wrong with the contract, one message each, naming the port or case at fault; empty when it is
        // well formed. A malformed contract would otherwise only show up as a part that never scores.
        public IReadOnlyList<string> Problems()
        {
            var problems = new List<string>();
            if (string.IsNullOrWhiteSpace(Name))
            {
                problems.Add("The contract has no name.");
            }
            List<Port> ports = new[] { Start }.Concat(Done ?? Array.Empty<Port>()).Concat(Data ?? Array.Empty<Port>()).Where(port => port != null).ToList();
            foreach (IGrouping<string, Port> repeated in ports.GroupBy(port => port.Name).Where(group => group.Count() > 1))
            {
                problems.Add($"Port name '{repeated.Key}' is used {repeated.Count()} times; port names must be unique.");
            }
            foreach (Port port in ports)
            {
                problems.AddRange(WidthProblems(port));
            }
            if (Start == null)
            {
                problems.Add("The contract has no start port.");
            }
            else if (Start.Direction != PortDirection.In || Start.Kind != PortKind.Trigger)
            {
                problems.Add($"Start port '{Start.Name}' must be an in-port of kind Trigger.");
            }
            foreach (Port extraStart in DataIn.Where(port => port.Kind == PortKind.Trigger))
            {
                problems.Add($"Data in-port '{extraStart.Name}' is a trigger, which would be a second start; a part has exactly one start.");
            }
            if (Done == null || Done.Count == 0)
            {
                problems.Add("The contract has no done port; it needs at least one.");
            }
            foreach (Port done in Done ?? Array.Empty<Port>())
            {
                if (done.Direction != PortDirection.Out || done.Kind != PortKind.Trigger)
                {
                    problems.Add($"Done port '{done.Name}' must be an out-port of kind Trigger.");
                }
            }
            if (MaxLatency < 1)
            {
                problems.Add($"The maximum latency is {MaxLatency}; it must be at least 1 step.");
            }
            if (MinLatency < 0 || MinLatency > MaxLatency)
            {
                problems.Add($"The minimum latency is {MinLatency}; it must be between 0 and the maximum latency {MaxLatency}.");
            }
            if (Cases == null || Cases.Count == 0)
            {
                problems.Add("The contract has no test cases.");
            }
            for (int index = 0; index < (Cases?.Count ?? 0); index++)
            {
                problems.AddRange(CaseProblems(Cases![index], $"Case {index + 1}"));
            }
            return problems;
        }

        public bool IsValid => Problems().Count == 0;

        // Throws ArgumentException listing every problem.
        public Contract Validated()
        {
            IReadOnlyList<string> problems = Problems();
            if (problems.Count > 0)
            {
                throw new ArgumentException($"Contract '{Name}' is malformed: " + string.Join(" ", problems));
            }
            return this;
        }

        private static IEnumerable<string> WidthProblems(Port port)
        {
            if (port.Kind == PortKind.Binary && (port.Width < 1 || port.Width > Port.MaxBinaryWidth))
            {
                yield return $"Binary port '{port.Name}' has width {port.Width}; it must be from 1 to {Port.MaxBinaryWidth}.";
            }
            if (port.Kind != PortKind.Binary && port.Width != 0)
            {
                yield return $"Port '{port.Name}' is not binary, so its width must be 0, not {port.Width}.";
            }
        }

        private IEnumerable<string> CaseProblems(ContractCase @case, string label)
        {
            if (@case == null)
            {
                yield return $"{label} is missing.";
                yield break;
            }
            foreach (string problem in ValueProblems(@case.Inputs, DataIn.ToList(), label, "in-port"))
            {
                yield return problem;
            }
            foreach (string problem in ValueProblems(@case.Outputs, DataOut.ToList(), label, "out-port"))
            {
                yield return problem;
            }
            if (Done != null && Done.All(done => done.Name != @case.Done))
            {
                yield return $"{label} expects done port '{@case.Done}', which is not one of the done ports.";
            }
        }

        private static IEnumerable<string> ValueProblems(IReadOnlyDictionary<string, int>? values, List<Port> ports, string label, string role)
        {
            values ??= new Dictionary<string, int>();
            foreach (Port port in ports.Where(port => !values.ContainsKey(port.Name)))
            {
                yield return $"{label} gives no value for data {role} '{port.Name}'.";
            }
            foreach (string name in values.Keys.Where(name => ports.All(port => port.Name != name)))
            {
                yield return $"{label} gives a value for '{name}', which is not a data {role}.";
            }
            foreach (Port port in ports.Where(port => values.ContainsKey(port.Name)))
            {
                int value = values[port.Name];
                string? problem = port.Kind switch
                {
                    _ when value < 0 => "is negative",
                    PortKind.Interval when value < 1 => "is not a valid interval, which must be at least 1",
                    PortKind.Trigger when value > 1 => "is not a valid trigger value, which must be 0 or 1",
                    PortKind.Binary when port.Width is >= 1 and <= Port.MaxBinaryWidth && value >= 1 << port.Width => $"does not fit in {port.Width} bits",
                    _ => null,
                };
                if (problem != null)
                {
                    yield return $"{label} gives {value} for '{port.Name}', which {problem}.";
                }
            }
        }
    }
}
