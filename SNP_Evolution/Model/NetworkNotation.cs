using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SnpEvolution.Model
{
    // Renders a network as a text table in SN P notation, with spike counts written back out as runs of 'a'.
    public static class NetworkNotation
    {
        private const int MaxSpelledOutSpikes = 10;
        private const string ColumnGap = "   ";

        // "aaa" for small counts, "a^250" once a run gets too long to read, and "-" for none.
        public static string Spikes(long count) =>
            count == 0 ? "-" : count <= MaxSpelledOutSpikes ? new string('a', (int)count) : "a^" + count;

        // "aa -> a" fires, "aa -> forget" consumes without emitting, ";d" shows a delay and ";d axonal" an axonal one.
        // Standard rules add the spikes they consume, "a(aa)*/a -> aa", leaving out "E/" when E only matches the consumed count.
        public static string Rule(Rule rule) =>
            $"{Condition(rule)} -> {(rule.Fire ? Spikes(rule.Sends) : "forget")}{(rule.Delay > 0 ? ";" + rule.Delay + (rule.DelayKind == DelayKind.Axonal ? " axonal" : "") : "")}";

        private static string Condition(Rule rule)
        {
            if (rule.Consume is not long consume)
            {
                return rule.Expression;
            }
            string consumed = Spikes(consume);
            return rule.Expression == new string('a', (int)Math.Min(consume, MaxSpelledOutSpikes + 1)) ? consumed : rule.Expression + "/" + consumed;
        }

        public static string Format(Network network) => Format(network, network.Neurons.Select(neuron => neuron.InitialSpikes).ToList());

        // Formats the network holding the given spike counts, such as a simulation part-way through.
        public static string Format(Network network, IReadOnlyList<long> spikes)
        {
            var rows = new List<string[]> { new[] { "Neuron", "Spikes", "Rules", "Sends to" } };
            for (int index = 0; index < network.Neurons.Count; index++)
            {
                Neuron neuron = network.Neurons[index];
                rows.Add(new[]
                {
                    Name(index + 1) + (neuron.IsInput ? " (in)" : "") + (neuron.IsOutput ? " (out)" : "") + (neuron.Module is ModuleTag tag ? $" [module {tag.Module}]" : ""),
                    Spikes(spikes[index]),
                    string.Join("  |  ", neuron.Rules.Select(Rule)),
                    neuron.Connections.Count == 0 ? "-" : string.Join(", ", neuron.Connections.Select(Name)),
                });
            }
            int[] widths = Enumerable.Range(0, rows[0].Length).Select(column => rows.Max(row => row[column].Length)).ToArray();
            var text = new StringBuilder();
            foreach (string[] row in rows)
            {
                text.AppendLine(string.Join(ColumnGap, row.Select((cell, column) => cell.PadRight(widths[column]))).TrimEnd());
            }
            return text.ToString();
        }

        private static string Name(int position) => "n" + position;
    }
}
