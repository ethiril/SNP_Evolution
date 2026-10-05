using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SnpEvolution.Networks
{
    // Renders a network as a text table in SN P notation, with spike counts written back out as runs of 'a'.
    public static class NetworkNotation
    {
        private const int MaxSpelledOutSpikes = 10;
        private const string ColumnGap = "   ";

        // "aaa" for small counts, "a^250" once a run gets too long to read, and "-" for none.
        public static string Spikes(long count) =>
            count == 0 ? "-" : count <= MaxSpelledOutSpikes ? new string('a', (int)count) : "a^" + count;

        // "aa -> a" fires, "aa -> forget" consumes without emitting, and ";d" shows a delay.
        public static string Rule(Rule rule) =>
            $"{rule.Expression} -> {(rule.Fire ? "a" : "forget")}{(rule.Delay > 0 ? ";" + rule.Delay : "")}";

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
                    Name(index + 1) + (neuron.IsOutput ? " (out)" : ""),
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
