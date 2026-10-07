using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Specs.Verification
{
    // A counterexample as the verify command and the admission log print it: the input, the rule it breaks, what the
    // contract expects and what was read, then the steps of the failing computation.
    public static class CounterexampleText
    {
        public static string Of(Part part, Counterexample counterexample)
        {
            var task = new ContractTask(counterexample.Contract, part.Binding);
            int caseIndex = counterexample.Contract.Cases.ToList().IndexOf(counterexample.Case);
            ContractCase @case = counterexample.Case;
            string expected = string.Join(",", new[] { @case.Done }.Concat(@case.Outputs.Select(pair => $"{pair.Key}={pair.Value}")));
            string trace = SpikeTrace.Choices(part.Network).Count == 0
                ? Table(part, SpikeTrace.Run(part.Network, task.Cases[caseIndex].Input, task.StepsNeeded))
                : Firings(part, counterexample.Run);
            return $"Counterexample at {counterexample.Inputs}: {ContractTask.RuleName(counterexample.Rule)} fails. Expected {expected}; read {counterexample.Read}.\n{trace}";
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
            List<Port> watched = PortLayout.OutPorts(part.Contract).ToList();
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
