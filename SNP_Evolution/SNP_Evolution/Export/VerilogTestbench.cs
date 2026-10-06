using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Export
{
    // A testbench for an exported design, and the output our own engine says it should print.
    public static class VerilogTestbench
    {
        // Prints "step neuron sent held" as SpikeTrace.SentAndHeld does, so the two outputs compare as text.
        public static string For(VerilogDesign design, IReadOnlyList<InputSpikes> cases, IReadOnlyList<int> steps)
        {
            List<NetworkPort> inputs = design.Ports.Where(port => port.IsInput).ToList();
            List<NetworkPort> outputs = design.Ports.Where(port => !port.IsInput).ToList();
            var text = new StringBuilder();
            text.AppendLine("`timescale 1ns/1ns");
            text.AppendLine($"module {design.Name}_tb;");
            text.AppendLine("    reg clk = 0;");
            text.AppendLine("    reg rst = 1;");
            text.AppendLine("    integer t;");
            foreach (NetworkPort input in inputs)
            {
                text.AppendLine($"    reg [{design.CounterWidth - 1}:0] {input.Name} = 0;");
            }
            foreach (NetworkPort output in outputs)
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
    }
}
