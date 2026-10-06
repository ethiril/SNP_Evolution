using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Export
{
    // A network port of the exported module: the environment's spikes into an input neuron, or what a neuron sends.
    public sealed record VerilogPort(string Name, int Neuron, bool IsInput);

    // Module is the synthesisable source: a top module named Name and one module per neuron. CounterWidth bits hold
    // a neuron's spikes, and SentWidth bits what it sends on one step.
    public sealed record VerilogDesign(string Name, string Module, int CounterWidth, int SentWidth, int NeuronCount, IReadOnlyList<VerilogPort> Ports);

    // Writes a deterministic network as Verilog that steps exactly as NetworkSimulation does, one clock per step. Each
    // neuron is a spike counter, its rule conditions as lasso lookups (SpikeCondition: a tail table, then a period),
    // and only the state its rules need: the countdown and pending flag of a delayed legacy rule, the countdown, closed
    // flag and pending spikes of a delayed standard rule, and a shift register of spikes in flight for axonal delays.
    // A network that could choose between rules is refused, since hardware would need a policy for the choice.
    public static class VerilogExporter
    {
        private static readonly HashSet<string> Reserved = new HashSet<string> { "clk", "rst", "overflow", "module", "input", "output", "wire", "reg", "begin", "end", "case" };

        // The part's ports by contract name, with counters wide enough for the most spikes any neuron held on any case.
        public static VerilogDesign Export(Part part) =>
            Export(part.Network, part.Contract.Name, part.Ports().Select(port => new VerilogPort(port.Port.Name, port.Position, port.Port.Direction == PortDirection.In)).ToList(),
                PartEvolution.Measure(part).Cost.RegisterWidth);

        // A network with no contract has inputs in1, in2, ... and outputs out (out1, out2, ... for several output neurons).
        public static IReadOnlyList<VerilogPort> PlainPorts(Network network)
        {
            List<int> inputs = network.Neurons.Select((neuron, index) => (neuron, index)).Where(pair => pair.neuron.IsInput).Select(pair => pair.index + 1).ToList();
            List<int> outputs = network.Neurons.Select((neuron, index) => (neuron, index)).Where(pair => pair.neuron.IsOutput).Select(pair => pair.index + 1).ToList();
            return inputs.Select((neuron, index) => new VerilogPort($"in{index + 1}", neuron, true))
                .Concat(outputs.Select((neuron, index) => new VerilogPort(outputs.Count == 1 ? "out" : $"out{index + 1}", neuron, false)))
                .ToList();
        }

        // mostHeld sizes the counters; HardwareCost.RegisterWidth gives it for a part. A run that holds more sets overflow.
        public static VerilogDesign Export(Network network, string name, IReadOnlyList<VerilogPort> ports, long mostHeld)
        {
            if (SpikeTrace.Choices(network).FirstOrDefault() is string choice)
            {
                throw new ArgumentException($"Only deterministic networks export to Verilog, since hardware cannot choose between rules. {choice}");
            }
            string module = Identifier(name);
            CompiledNetwork compiled = CompiledNetwork.Of(network);
            int counterWidth = BitsFor(Math.Max(mostHeld, network.Neurons.Select(neuron => neuron.InitialSpikes).DefaultIfEmpty(0).Max()));
            int sentWidth = BitsFor(Enumerable.Range(0, network.Neurons.Count).Select(neuron => 2L * MostProduced(compiled, neuron)).DefaultIfEmpty(1).Max());
            int delayWidth = BitsFor(network.Neurons.SelectMany(neuron => neuron.Rules).Select(rule => (long)rule.Delay).DefaultIfEmpty(1).Max());
            List<VerilogPort> named = ports.Select(port => port with { Name = PortName(port.Name) }).ToList();
            var text = new StringBuilder();
            text.AppendLine($"// {name}: {network.Neurons.Count} neurons, exported from SNP_Evolution. One clock is one step of the SN P system.");
            text.AppendLine("// Each step every neuron applies its one applicable rule, if any, then the spikes sent this step are delivered");
            text.AppendLine("// to every open target along with the environment's spikes; a closed neuron loses what is sent to it.");
            text.AppendLine();
            text.Append(TopModule(network, module, named, counterWidth, sentWidth));
            for (int neuron = 0; neuron < network.Neurons.Count; neuron++)
            {
                text.AppendLine();
                text.Append(NeuronModule(network, compiled, module, neuron, counterWidth, sentWidth, delayWidth));
            }
            return new VerilogDesign(module, text.ToString(), counterWidth, sentWidth, network.Neurons.Count, named);
        }

        // Drives every case from reset and prints, per step and neuron, "step neuron sent held" as SpikeTrace.SentAndHeld
        // does, each case under a "case k" line, and finally whether any counter overflowed.
        public static string Testbench(VerilogDesign design, IReadOnlyList<InputSpikes> cases, IReadOnlyList<int> steps)
        {
            List<VerilogPort> inputs = design.Ports.Where(port => port.IsInput).ToList();
            List<VerilogPort> outputs = design.Ports.Where(port => !port.IsInput).ToList();
            var text = new StringBuilder();
            text.AppendLine("`timescale 1ns/1ns");
            text.AppendLine($"module {design.Name}_tb;");
            text.AppendLine("    reg clk = 0;");
            text.AppendLine("    reg rst = 1;");
            text.AppendLine("    integer t;");
            foreach (VerilogPort input in inputs)
            {
                text.AppendLine($"    reg [{design.CounterWidth - 1}:0] {input.Name} = 0;");
            }
            foreach (VerilogPort output in outputs)
            {
                text.AppendLine($"    wire [{design.SentWidth - 1}:0] {output.Name};");
            }
            text.AppendLine("    wire overflow;");
            for (int neuron = 1; neuron <= design.NeuronCount; neuron++)
            {
                text.AppendLine($"    reg [{design.SentWidth - 1}:0] sent_{neuron};");
            }
            string connections = string.Join(", ", new[] { ".clk(clk)", ".rst(rst)" }.Concat(design.Ports.Select(port => $".{port.Name}({port.Name})")).Append(".overflow(overflow)"));
            text.AppendLine($"    {design.Name} dut ({connections});");
            text.AppendLine();
            text.AppendLine("    task tick;");
            text.AppendLine("        begin");
            text.AppendLine("            #1 clk = 1;");
            text.AppendLine("            #1 clk = 0;");
            text.AppendLine("        end");
            text.AppendLine("    endtask");
            text.AppendLine();
            text.AppendLine("    // What each neuron sends is read before the clock, what it holds after it.");
            text.AppendLine("    task step;");
            text.AppendLine("        begin");
            text.AppendLine("            #1;");
            for (int neuron = 1; neuron <= design.NeuronCount; neuron++)
            {
                text.AppendLine($"            sent_{neuron} = dut.n{neuron}.sent;");
            }
            text.AppendLine("            tick;");
            for (int neuron = 1; neuron <= design.NeuronCount; neuron++)
            {
                text.AppendLine($"            $display(\"%0d {neuron} %0d %0d\", t, sent_{neuron}, dut.n{neuron}.count);");
            }
            text.AppendLine("        end");
            text.AppendLine("    endtask");
            text.AppendLine();
            text.AppendLine("    initial begin");
            for (int index = 0; index < cases.Count; index++)
            {
                text.AppendLine($"        $display(\"case {index + 1}\");");
                text.AppendLine($"        {string.Concat(inputs.Select(input => $"{input.Name} = 0; "))}rst = 1;");
                text.AppendLine("        tick;");
                text.AppendLine("        rst = 0;");
                text.AppendLine($"        for (t = 0; t < {steps[index]}; t = t + 1) begin");
                for (int input = 0; input < inputs.Count; input++)
                {
                    IReadOnlyList<int> spikes = input < cases[index].StepsPerInput.Count ? cases[index].StepsPerInput[input] : Array.Empty<int>();
                    string arriving = spikes.Count == 0 ? "0" : string.Join(" + ", spikes.Select(step => $"(t == {step})"));
                    text.AppendLine($"            {inputs[input].Name} = {arriving};");
                }
                text.AppendLine("            step;");
                text.AppendLine("        end");
            }
            text.AppendLine("        $display(\"overflow %0d\", overflow);");
            text.AppendLine("        $finish;");
            text.AppendLine("    end");
            text.AppendLine("endmodule");
            return text.ToString();
        }

        // What the testbench should print, from our own simulation of the same cases.
        public static string ExpectedOutput(Network network, IReadOnlyList<InputSpikes> cases, IReadOnlyList<int> steps)
        {
            var text = new StringBuilder();
            for (int index = 0; index < cases.Count; index++)
            {
                text.Append($"case {index + 1}\n");
                text.Append(SpikeTrace.Run(network, cases[index], steps[index]).SentAndHeld());
            }
            return text.Append("overflow 0\n").ToString();
        }

        public static int BitsFor(long value) => Math.Max(1, 64 - System.Numerics.BitOperations.LeadingZeroCount((ulong)Math.Max(0, value)));

        // The most a neuron's rules send in all, which bounds what it sends on one step: an axonal delivery plus one rule's spikes.
        private static long MostProduced(CompiledNetwork network, int neuron)
        {
            long total = 0;
            for (int rule = network.RuleStart[neuron]; rule < network.RuleStart[neuron + 1]; rule++)
            {
                total += network.RuleProduce[rule];
            }
            return Math.Max(1, total);
        }

        private static string TopModule(Network network, string module, IReadOnlyList<VerilogPort> ports, int counterWidth, int sentWidth)
        {
            var text = new StringBuilder();
            text.AppendLine($"module {module} (");
            text.AppendLine("    input  wire clk,");
            text.AppendLine("    input  wire rst,    // synchronous: loads every neuron's initial spikes");
            foreach (VerilogPort port in ports)
            {
                text.AppendLine(port.IsInput
                    ? $"    input  wire [{counterWidth - 1}:0] {port.Name},    // spikes arriving at n{port.Neuron} this step"
                    : $"    output wire [{sentWidth - 1}:0] {port.Name},    // spikes n{port.Neuron} sends this step");
            }
            text.AppendLine("    output wire overflow    // set once a counter would need more bits than it has");
            text.AppendLine(");");
            int count = network.Neurons.Count;
            for (int neuron = 1; neuron <= count; neuron++)
            {
                text.AppendLine($"    wire [{sentWidth - 1}:0] sent_{neuron};");
            }
            text.AppendLine($"    wire [{count - 1}:0] overflows;");
            text.AppendLine();
            for (int neuron = 1; neuron <= count; neuron++)
            {
                List<string> terms = Enumerable.Range(1, count).Where(source => network.Neurons[source - 1].Connections.Contains(neuron)).Select(source => $"sent_{source}").ToList();
                terms.AddRange(ports.Where(port => port.IsInput && port.Neuron == neuron).Select(port => port.Name));
                int incomingWidth = IncomingWidth(network, neuron - 1, counterWidth, sentWidth);
                text.AppendLine($"    wire [{incomingWidth - 1}:0] incoming_{neuron} = {(terms.Count == 0 ? "0" : string.Join(" + ", terms))};");
                text.AppendLine($"    {module}_n{neuron} n{neuron} (.clk(clk), .rst(rst), .incoming(incoming_{neuron}), .sent(sent_{neuron}), .count(), .overflow(overflows[{neuron - 1}]));");
            }
            text.AppendLine();
            foreach (VerilogPort port in ports.Where(port => !port.IsInput))
            {
                text.AppendLine($"    assign {port.Name} = sent_{port.Neuron};");
            }
            text.AppendLine("    assign overflow = |overflows;");
            text.AppendLine("endmodule");
            return text.ToString();
        }

        private static string NeuronModule(Network network, CompiledNetwork compiled, string module, int index, int counterWidth, int sentWidth, int delayWidth)
        {
            Neuron neuron = network.Neurons[index];
            int incomingWidth = IncomingWidth(network, index, counterWidth, sentWidth);
            List<Rule> rules = neuron.Rules.ToList();
            bool holds = rules.Any(rule => rule.Delay > 0 && !rule.Axonal && !rule.IsStandard);
            bool closes = rules.Any(rule => rule.Delay > 0 && !rule.Axonal && rule.IsStandard);
            int flight = rules.Where(rule => rule.Axonal).Select(rule => rule.Delay).DefaultIfEmpty(0).Max();
            int totalWidth = Math.Max(counterWidth, incomingWidth) + 1;
            var text = new StringBuilder();
            text.AppendLine($"// n{index + 1}: {(rules.Count == 0 ? "no rules" : string.Join("  |  ", rules.Select(NetworkNotation.Rule)))}; " +
                $"sends to {(neuron.Connections.Count == 0 ? "nothing" : string.Join(", ", neuron.Connections.Select(target => $"n{target}")))}");
            text.AppendLine($"module {module}_n{index + 1} (");
            text.AppendLine("    input  wire clk,");
            text.AppendLine("    input  wire rst,");
            text.AppendLine($"    input  wire [{incomingWidth - 1}:0] incoming,");
            text.AppendLine($"    output reg  [{sentWidth - 1}:0] sent,");
            text.AppendLine($"    output reg  [{counterWidth - 1}:0] count,");
            text.AppendLine("    output reg  overflow");
            text.AppendLine(");");
            text.AppendLine($"    localparam INITIAL = {neuron.InitialSpikes};");
            text.AppendLine($"    localparam [{totalWidth - 1}:0] MOST = {(1L << counterWidth) - 1};");
            for (int rule = 0; rule < rules.Count; rule++)
            {
                SpikeCondition condition = rules[rule].Condition;
                ReadOnlySpan<bool> accepts = condition.Accepts;
                var bits = new StringBuilder();
                for (int position = accepts.Length - 1; position >= 0; position--)
                {
                    bits.Append(accepts[position] ? '1' : '0');
                }
                text.AppendLine($"    // Rule {rule + 1}, {NetworkNotation.Rule(rules[rule])}: bit i of ACCEPTS says whether lasso position i matches,");
                text.AppendLine($"    // and a count past the tail of {condition.TailLength} goes round the period of {condition.Period}.");
                text.AppendLine($"    localparam R{rule + 1}_TAIL = {condition.TailLength};");
                text.AppendLine($"    localparam R{rule + 1}_PERIOD = {condition.Period};");
                text.AppendLine($"    localparam [{accepts.Length - 1}:0] R{rule + 1}_ACCEPTS = {accepts.Length}'b{bits};");
            }
            text.AppendLine();
            if (holds)
            {
                text.AppendLine($"    reg [{delayWidth - 1}:0] hold_for, next_hold_for;   // a delayed legacy rule has sent; the neuron waits, still receiving");
                text.AppendLine("    reg pending, next_pending;                  // and empties when the wait is over");
            }
            if (closes)
            {
                text.AppendLine($"    reg [{delayWidth - 1}:0] closed_for, next_closed_for; // a delayed standard rule has consumed; the neuron is closed");
                text.AppendLine($"    reg [{sentWidth - 1}:0] pending_spikes, next_pending_spikes; // and sends these when it reopens");
            }
            for (int ahead = 0; ahead < flight; ahead++)
            {
                text.AppendLine($"    reg [{sentWidth - 1}:0] flight_{ahead}, next_flight_{ahead};{(ahead == 0 ? "   // spikes on the axon, flight_k leaving k steps from now" : "")}");
            }
            text.AppendLine($"    reg [{counterWidth - 1}:0] next_count;");
            text.AppendLine($"    reg [{sentWidth - 1}:0] own;");
            if (closes)
            {
                text.AppendLine("    reg closed;");
            }
            text.AppendLine($"    reg [{totalWidth - 1}:0] total;");
            string busy = string.Join(" || ", new[] { holds ? "hold_for != 0" : null, closes ? "closed_for != 0" : null }.Where(term => term != null));
            for (int rule = 0; rule < rules.Count; rule++)
            {
                string r = $"R{rule + 1}";
                string matches = $"((count < {r}_TAIL) ? {r}_ACCEPTS[count] : {r}_ACCEPTS[{r}_TAIL + (count - {r}_TAIL) % {r}_PERIOD])";
                string enough = rules[rule].Consume is long consume ? $"count >= {consume} && " : "";
                text.AppendLine($"    wire r{rule + 1} = {(busy.Length == 0 ? "" : $"!({busy}) && ")}{enough}{matches};");
            }
            text.AppendLine();
            text.AppendLine("    always @* begin");
            text.AppendLine("        next_count = count;");
            if (holds)
            {
                text.AppendLine("        next_hold_for = hold_for;");
                text.AppendLine("        next_pending = pending;");
            }
            if (closes)
            {
                text.AppendLine("        next_closed_for = closed_for;");
                text.AppendLine("        next_pending_spikes = pending_spikes;");
            }
            for (int ahead = 0; ahead < flight; ahead++)
            {
                text.AppendLine($"        next_flight_{ahead} = {(ahead + 1 < flight ? $"flight_{ahead + 1}" : "0")};");
            }
            text.AppendLine("        own = 0;");
            if (closes)
            {
                text.AppendLine("        closed = 0;");
            }
            string keyword = "if";
            if (holds)
            {
                text.AppendLine("        if (hold_for != 0) begin");
                text.AppendLine("            next_hold_for = hold_for - 1;");
                keyword = "end else if";
            }
            if (closes)
            {
                text.AppendLine($"        {keyword} (closed_for != 0) begin");
                text.AppendLine("            next_closed_for = closed_for - 1;");
                text.AppendLine("            if (closed_for != 1) begin");
                text.AppendLine("                closed = 1;");
                text.AppendLine("            end else begin");
                text.AppendLine("                own = pending_spikes;");
                text.AppendLine("                next_pending_spikes = 0;");
                text.AppendLine("            end");
                keyword = "end else if";
            }
            if (holds)
            {
                text.AppendLine($"        {keyword} (pending) begin");
                text.AppendLine("            // As in the original program, a rule chosen on this step still sends, and the delayed rule empties the neuron.");
                for (int rule = 0; rule < rules.Count; rule++)
                {
                    text.AppendLine($"            {(rule == 0 ? "if" : "else if")} (r{rule + 1}) own = {compiled.RuleProduce[compiled.RuleStart[index] + rule]};");
                }
                text.AppendLine("            next_count = 0;");
                text.AppendLine("            next_pending = 0;");
                keyword = "end else if";
            }
            for (int rule = 0; rule < rules.Count; rule++)
            {
                text.AppendLine($"        {keyword} (r{rule + 1}) begin");
                foreach (string line in RuleAction(rules[rule], compiled.RuleProduce[compiled.RuleStart[index] + rule]))
                {
                    text.AppendLine("            " + line);
                }
                keyword = "end else if";
            }
            if (keyword != "if")
            {
                text.AppendLine("        end");
            }
            text.AppendLine($"        sent = own{(flight > 0 ? " + flight_0" : "")};");
            text.AppendLine(closes ? "        total = next_count + (closed ? 0 : incoming);" : "        total = next_count + incoming;");
            text.AppendLine("    end");
            text.AppendLine();
            text.AppendLine("    always @(posedge clk) begin");
            text.AppendLine("        if (rst) begin");
            text.AppendLine("            count <= INITIAL;");
            text.AppendLine("            overflow <= 0;");
            if (holds)
            {
                text.AppendLine("            hold_for <= 0;");
                text.AppendLine("            pending <= 0;");
            }
            if (closes)
            {
                text.AppendLine("            closed_for <= 0;");
                text.AppendLine("            pending_spikes <= 0;");
            }
            for (int ahead = 0; ahead < flight; ahead++)
            {
                text.AppendLine($"            flight_{ahead} <= 0;");
            }
            text.AppendLine("        end else begin");
            text.AppendLine($"            count <= total[{counterWidth - 1}:0];");
            text.AppendLine("            overflow <= overflow || total > MOST;");
            if (holds)
            {
                text.AppendLine("            hold_for <= next_hold_for;");
                text.AppendLine("            pending <= next_pending;");
            }
            if (closes)
            {
                text.AppendLine("            closed_for <= next_closed_for;");
                text.AppendLine("            pending_spikes <= next_pending_spikes;");
            }
            for (int ahead = 0; ahead < flight; ahead++)
            {
                text.AppendLine($"            flight_{ahead} <= next_flight_{ahead};");
            }
            text.AppendLine("        end");
            text.AppendLine("    end");
            text.AppendLine("endmodule");
            return text.ToString();
        }

        // What applying the rule does, as NetworkSimulation.ReleaseSpikes does it.
        private static IEnumerable<string> RuleAction(Rule rule, int produce)
        {
            string consumed = rule.Consume is long consume ? $"count - {consume}" : "0";
            if (rule.Axonal)
            {
                yield return $"next_count = {consumed};";
                yield return $"next_flight_{rule.Delay - 1} = next_flight_{rule.Delay - 1} + {produce};";
            }
            else if (rule.Delay > 0 && !rule.IsStandard)
            {
                yield return $"own = {produce};";
                yield return $"next_hold_for = {rule.Delay};";
                yield return "next_pending = 1;";
            }
            else if (rule.Delay > 0)
            {
                yield return $"next_count = {consumed};";
                yield return $"next_closed_for = {rule.Delay};";
                yield return $"next_pending_spikes = {produce};";
                yield return "closed = 1;";
            }
            else
            {
                yield return $"own = {produce};";
                yield return $"next_count = {consumed};";
            }
        }

        // Wide enough for every sender's spikes plus the environment's at once.
        private static int IncomingWidth(Network network, int index, int counterWidth, int sentWidth)
        {
            long senders = network.Neurons.Count(neuron => neuron.Connections.Contains(index + 1));
            long most = senders * ((1L << sentWidth) - 1) + (network.Neurons[index].IsInput ? (1L << counterWidth) - 1 : 0);
            return BitsFor(Math.Max(1, most));
        }

        private static string PortName(string name)
        {
            string identifier = Identifier(name);
            return Reserved.Contains(identifier) ? "port_" + identifier : identifier;
        }

        // Letters, digits and underscores, starting with a letter: "delay 2" becomes delay_2.
        private static string Identifier(string name)
        {
            string identifier = new string(name.Select(letter => char.IsAsciiLetterOrDigit(letter) ? letter : '_').ToArray()).Trim('_');
            return identifier.Length == 0 || !char.IsAsciiLetter(identifier[0]) ? "snp_" + identifier : identifier;
        }
    }
}
