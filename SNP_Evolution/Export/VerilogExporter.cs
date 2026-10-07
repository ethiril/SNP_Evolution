using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SnpEvolution.Model;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Verification;

namespace SnpEvolution.Export
{
    // Module holds a top module named Name and one module per neuron.
    public sealed record VerilogDesign(string Name, string Module, int CounterWidth, int SentWidth, int NeuronCount, IReadOnlyList<NetworkPort> Ports);

    // Writes a deterministic network as Verilog that steps exactly as NetworkSimulation does, one clock per step; a rule choice is refused, since hardware would need a policy for it.
    public static partial class VerilogExporter
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
