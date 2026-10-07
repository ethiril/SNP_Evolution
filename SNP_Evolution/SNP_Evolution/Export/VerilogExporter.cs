using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Verification;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Export
{
    // Module holds a top module named Name and one module per neuron.
    public sealed record VerilogDesign(string Name, string Module, int CounterWidth, int SentWidth, int NeuronCount, IReadOnlyList<NetworkPort> Ports);

    // Writes a deterministic network as Verilog that steps exactly as NetworkSimulation does, one clock per step; a rule choice is refused, since hardware would need a policy for it.
    public static class VerilogExporter
    {
        private static readonly HashSet<string> Reserved = new HashSet<string> { "clk", "rst", "overflow", "module", "input", "output", "wire", "reg", "begin", "end", "case" };

        // The part's ports by contract name, with counters wide enough for the most spikes any neuron held on any case.
        public static VerilogDesign Export(Part part) => Export(part.Network, part.Contract.Name, NetworkPort.ForPart(part), Verifier.Measure(part, new EvaluationBudget()).Cost.RegisterWidth);

        // mostHeld sizes the counters; HardwareCost.RegisterWidth gives it for a part. A run that holds more sets overflow.
        public static VerilogDesign Export(Network network, string name, IReadOnlyList<NetworkPort> ports, long mostHeld)
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
            List<NetworkPort> named = ports.Select(port => port with { Name = PortName(port.Name) }).ToList();
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

        private static string TopModule(Network network, string module, IReadOnlyList<NetworkPort> ports, int counterWidth, int sentWidth)
        {
            var text = new StringBuilder();
            text.AppendLine($"module {module} (");
            text.AppendLine("    input  wire clk,");
            text.AppendLine("    input  wire rst,    // synchronous: loads every neuron's initial spikes");
            foreach (NetworkPort port in ports)
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
            foreach (NetworkPort port in ports.Where(port => !port.IsInput))
            {
                text.AppendLine($"    assign {port.Name} = sent_{port.Neuron};");
            }
            text.AppendLine("    assign overflow = |overflows;");
            text.AppendLine("endmodule");
            return text.ToString();
        }

        // What a neuron's rules need from its module: hold_for/pending for a delayed legacy rule, closed_for/pending_spikes for a
        // delayed standard rule, and Flight axon registers for axonal delays.
        private sealed record NeuronShape(int Index, Neuron Neuron, IReadOnlyList<Rule> Rules, IReadOnlyList<int> Produce, bool Holds, bool Closes, int Flight)
        {
            public static NeuronShape Of(Network network, CompiledNetwork compiled, int index)
            {
                Neuron neuron = network.Neurons[index];
                List<Rule> rules = neuron.Rules.ToList();
                return new NeuronShape(
                    index,
                    neuron,
                    rules,
                    rules.Select((_, rule) => compiled.RuleProduce[compiled.RuleStart[index] + rule]).ToList(),
                    rules.Any(rule => rule.DelayKind == DelayKind.Holding),
                    rules.Any(rule => rule.DelayKind == DelayKind.Closing),
                    rules.Where(rule => rule.DelayKind == DelayKind.Axonal).Select(rule => rule.Delay).DefaultIfEmpty(0).Max());
            }

            // Registers that reset to 0 and load next_NAME each clock.
            public IEnumerable<string> StateRegisters =>
                (Holds ? new[] { "hold_for", "pending" } : Array.Empty<string>())
                .Concat(Closes ? new[] { "closed_for", "pending_spikes" } : Array.Empty<string>())
                .Concat(Enumerable.Range(0, Flight).Select(ahead => $"flight_{ahead}"));
        }

        private static string NeuronModule(Network network, CompiledNetwork compiled, string module, int index, int counterWidth, int sentWidth, int delayWidth)
        {
            NeuronShape shape = NeuronShape.Of(network, compiled, index);
            int incomingWidth = IncomingWidth(network, index, counterWidth, sentWidth);
            int totalWidth = Math.Max(counterWidth, incomingWidth) + 1;
            var text = new StringBuilder();
            text.Append(Declarations(shape, module, incomingWidth, totalWidth, counterWidth, sentWidth, delayWidth));
            text.AppendLine();
            text.Append(StepLogic(shape));
            text.AppendLine();
            text.Append(ClockedState(shape, counterWidth));
            text.AppendLine("endmodule");
            return text.ToString();
        }

        private static string Declarations(NeuronShape shape, string module, int incomingWidth, int totalWidth, int counterWidth, int sentWidth, int delayWidth)
        {
            IReadOnlyList<Rule> rules = shape.Rules;
            IReadOnlyList<int> connections = shape.Neuron.Connections;
            var text = new StringBuilder();
            text.AppendLine($"// n{shape.Index + 1}: {(rules.Count == 0 ? "no rules" : string.Join("  |  ", rules.Select(NetworkNotation.Rule)))}; " +
                $"sends to {(connections.Count == 0 ? "nothing" : string.Join(", ", connections.Select(target => $"n{target}")))}");
            text.AppendLine($"module {module}_n{shape.Index + 1} (");
            text.AppendLine("    input  wire clk,");
            text.AppendLine("    input  wire rst,");
            text.AppendLine($"    input  wire [{incomingWidth - 1}:0] incoming,");
            text.AppendLine($"    output reg  [{sentWidth - 1}:0] sent,");
            text.AppendLine($"    output reg  [{counterWidth - 1}:0] count,");
            text.AppendLine("    output reg  overflow");
            text.AppendLine(");");
            text.AppendLine($"    localparam INITIAL = {shape.Neuron.InitialSpikes};");
            text.AppendLine($"    localparam [{totalWidth - 1}:0] MOST = {(1L << counterWidth) - 1};");
            for (int rule = 0; rule < rules.Count; rule++)
            {
                SpikeCondition condition = rules[rule].Condition;
                string bits = string.Concat(condition.Accepts.ToArray().Reverse().Select(accepted => accepted ? '1' : '0'));
                text.AppendLine($"    // Rule {rule + 1}, {NetworkNotation.Rule(rules[rule])}: bit i of ACCEPTS says whether lasso position i matches,");
                text.AppendLine($"    // and a count past the tail of {condition.TailLength} goes round the period of {condition.Period}.");
                text.AppendLine($"    localparam R{rule + 1}_TAIL = {condition.TailLength};");
                text.AppendLine($"    localparam R{rule + 1}_PERIOD = {condition.Period};");
                text.AppendLine($"    localparam [{bits.Length - 1}:0] R{rule + 1}_ACCEPTS = {bits.Length}'b{bits};");
            }
            text.AppendLine();
            if (shape.Holds)
            {
                text.AppendLine($"    reg [{delayWidth - 1}:0] hold_for, next_hold_for;   // a delayed legacy rule has sent; the neuron waits, still receiving");
                text.AppendLine("    reg pending, next_pending;                  // and empties when the wait is over");
            }
            if (shape.Closes)
            {
                text.AppendLine($"    reg [{delayWidth - 1}:0] closed_for, next_closed_for; // a delayed standard rule has consumed; the neuron is closed");
                text.AppendLine($"    reg [{sentWidth - 1}:0] pending_spikes, next_pending_spikes; // and sends these when it reopens");
                text.AppendLine("    reg closed;");
            }
            for (int ahead = 0; ahead < shape.Flight; ahead++)
            {
                text.AppendLine($"    reg [{sentWidth - 1}:0] flight_{ahead}, next_flight_{ahead};{(ahead == 0 ? "   // spikes on the axon, flight_k leaving k steps from now" : "")}");
            }
            text.AppendLine($"    reg [{counterWidth - 1}:0] next_count;");
            text.AppendLine($"    reg [{sentWidth - 1}:0] own;");
            text.AppendLine($"    reg [{totalWidth - 1}:0] total;");
            string busy = string.Join(" || ", new[] { shape.Holds ? "hold_for != 0" : null, shape.Closes ? "closed_for != 0" : null }.Where(term => term != null));
            for (int rule = 0; rule < rules.Count; rule++)
            {
                string r = $"R{rule + 1}";
                string matches = $"((count < {r}_TAIL) ? {r}_ACCEPTS[count] : {r}_ACCEPTS[{r}_TAIL + (count - {r}_TAIL) % {r}_PERIOD])";
                string enough = rules[rule].Consume is long consume ? $"count >= {consume} && " : "";
                text.AppendLine($"    wire r{rule + 1} = {(busy.Length == 0 ? "" : $"!({busy}) && ")}{enough}{matches};");
            }
            return text.ToString();
        }

        // The step itself, branch for branch as NetworkSimulation.ReleaseSpikes takes them.
        private static string StepLogic(NeuronShape shape)
        {
            var text = new StringBuilder();
            text.AppendLine("    always @* begin");
            text.AppendLine("        next_count = count;");
            foreach (string register in shape.StateRegisters.Where(register => !register.StartsWith("flight_")))
            {
                text.AppendLine($"        next_{register} = {register};");
            }
            for (int ahead = 0; ahead < shape.Flight; ahead++)
            {
                text.AppendLine($"        next_flight_{ahead} = {(ahead + 1 < shape.Flight ? $"flight_{ahead + 1}" : "0")};");
            }
            text.AppendLine("        own = 0;");
            if (shape.Closes)
            {
                text.AppendLine("        closed = 0;");
            }
            string keyword = "if";
            if (shape.Holds)
            {
                text.AppendLine("        if (hold_for != 0) begin");
                text.AppendLine("            next_hold_for = hold_for - 1;");
                keyword = "end else if";
            }
            if (shape.Closes)
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
            if (shape.Holds)
            {
                text.AppendLine($"        {keyword} (pending) begin");
                text.AppendLine("            // As in the original program, a rule chosen on this step still sends, and the delayed rule empties the neuron.");
                for (int rule = 0; rule < shape.Rules.Count; rule++)
                {
                    text.AppendLine($"            {(rule == 0 ? "if" : "else if")} (r{rule + 1}) own = {shape.Produce[rule]};");
                }
                text.AppendLine("            next_count = 0;");
                text.AppendLine("            next_pending = 0;");
                keyword = "end else if";
            }
            for (int rule = 0; rule < shape.Rules.Count; rule++)
            {
                text.AppendLine($"        {keyword} (r{rule + 1}) begin");
                foreach (string line in RuleAction(shape.Rules[rule], shape.Produce[rule]))
                {
                    text.AppendLine("            " + line);
                }
                keyword = "end else if";
            }
            if (keyword != "if")
            {
                text.AppendLine("        end");
            }
            text.AppendLine($"        sent = own{(shape.Flight > 0 ? " + flight_0" : "")};");
            text.AppendLine(shape.Closes ? "        total = next_count + (closed ? 0 : incoming);" : "        total = next_count + incoming;");
            text.AppendLine("    end");
            return text.ToString();
        }

        private static string ClockedState(NeuronShape shape, int counterWidth)
        {
            var text = new StringBuilder();
            text.AppendLine("    always @(posedge clk) begin");
            text.AppendLine("        if (rst) begin");
            text.AppendLine("            count <= INITIAL;");
            text.AppendLine("            overflow <= 0;");
            foreach (string register in shape.StateRegisters)
            {
                text.AppendLine($"            {register} <= 0;");
            }
            text.AppendLine("        end else begin");
            text.AppendLine($"            count <= total[{counterWidth - 1}:0];");
            text.AppendLine("            overflow <= overflow || total > MOST;");
            foreach (string register in shape.StateRegisters)
            {
                text.AppendLine($"            {register} <= next_{register};");
            }
            text.AppendLine("        end");
            text.AppendLine("    end");
            return text.ToString();
        }

        // What applying the rule does, as NetworkSimulation.ReleaseSpikes does it.
        private static IEnumerable<string> RuleAction(Rule rule, int produce)
        {
            string consumed = rule.Consume is long consume ? $"count - {consume}" : "0";
            switch (rule.DelayKind)
            {
                case DelayKind.Axonal:
                    yield return $"next_count = {consumed};";
                    yield return $"next_flight_{rule.Delay - 1} = next_flight_{rule.Delay - 1} + {produce};";
                    break;
                case DelayKind.Holding:
                    yield return $"own = {produce};";
                    yield return $"next_hold_for = {rule.Delay};";
                    yield return "next_pending = 1;";
                    break;
                case DelayKind.Closing:
                    yield return $"next_count = {consumed};";
                    yield return $"next_closed_for = {rule.Delay};";
                    yield return $"next_pending_spikes = {produce};";
                    yield return "closed = 1;";
                    break;
                default:
                    yield return $"own = {produce};";
                    yield return $"next_count = {consumed};";
                    break;
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
