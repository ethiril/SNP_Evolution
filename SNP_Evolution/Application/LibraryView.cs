using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SnpEvolution.Model;
using SnpEvolution.Specs.Contracts;
using SnpEvolution.Specs.Parts;

namespace SnpEvolution.Application
{
    // Reading a part library without opening its files: a table of the parts kept, with the first-part contracts that
    // have none yet, and each part in full.
    public static class LibraryView
    {
        public static string Table(IReadOnlyList<PartFile> parts)
        {
            var table = new List<string[]> { new[] { "Contract", "Neurons", "Synapses", "Rules", "Latency", "Proof", "Seed", "Evaluations", "File" } };
            table.AddRange(parts.OrderBy(file => file.Part.Contract.Name, StringComparer.Ordinal).Select(file => new[]
            {
                file.Part.Contract.Name,
                Number(file.Part.Cost.Neurons),
                Number(file.Part.Cost.Synapses),
                Number(file.Part.Cost.Rules),
                Number(file.Part.Latency),
                Proof(file.Part.Proven),
                Number(file.Part.Origin.Seed),
                Number(file.Part.Origin.Evaluations),
                System.IO.Path.GetFileName(file.Path),
            }));
            int[] widths = Enumerable.Range(0, table[0].Length).Select(column => table.Max(cells => cells[column].Length)).ToArray();
            var text = new StringBuilder();
            foreach (string[] cells in table)
            {
                text.AppendLine(string.Join("   ", cells.Select((cell, column) => cell.PadRight(widths[column]))).TrimEnd());
            }
            List<string> missing = Missing(parts);
            text.AppendLine(missing.Count == 0 ? "Every first-part contract has a part." : $"No part yet for: {string.Join(", ", missing)}.");
            return text.ToString();
        }

        // The first-part contracts the library has no part for, in the order the catalogue lists them.
        public static List<string> Missing(IReadOnlyList<PartFile> parts) =>
            FirstParts.Contracts.Select(contract => contract.Name).Where(name => parts.All(file => file.Part.Contract.Name != name)).ToList();

        public static string Describe(PartFile file)
        {
            LibraryPart part = file.Part;
            Contract contract = part.Contract;
            var text = new StringBuilder();
            text.AppendLine($"{contract.Name} ({file.Path})");
            if (FirstParts.All.FirstOrDefault(first => first.Contracts.Any(each => each.Name == contract.Name)) is FirstPart first)
            {
                text.AppendLine($"Goal: {first.Goal}");
            }
            text.AppendLine("Ports: " + string.Join(", ", part.Part.Ports().Select(port => $"{port.Port.Name} ({port.Port.Direction.ToString().ToLowerInvariant()}, {port.Port.Kind.ToString().ToLowerInvariant()}) on neuron {port.Position}")));
            text.AppendLine($"Latency: {part.Latency}, contract allows {contract.MinLatency} to {contract.MaxLatency}");
            text.AppendLine($"Cost: {part.Cost}");
            text.AppendLine($"Proof: {part.Proven?.ToString() ?? "not checked yet (run verify)"}");
            text.AppendLine($"Found by: {part.Origin.Run}, seed {part.Origin.Seed}, {part.Origin.Evaluations} evaluations");
            if (part.Recipe is PartRecipe recipe)
            {
                text.AppendLine($"Built from: {string.Join(", ", recipe.Children)}, with {recipe.Glue.Count} glue neuron(s)");
            }
            text.AppendLine("What it reads on each case:");
            foreach ((string label, string reading) in Readings(part))
            {
                text.AppendLine($"  {label}: {reading}");
            }
            text.AppendLine("Network:");
            text.Append(NetworkNotation.Format(part.Part.Network));
            return text.ToString();
        }

        // The behaviour key holds the contract's name, then each case's readings in case order.
        private static IEnumerable<(string Label, string Reading)> Readings(LibraryPart part)
        {
            string[] readings = part.Behaviour.Split(" | ").Skip(1).ToArray();
            return part.Contract.Cases.Select((@case, index) =>
            {
                string label = @case.Label(part.Contract.DataIn);
                return (label.Length > 0 ? label : $"case {index + 1}", index < readings.Length ? readings[index] : "?");
            });
        }

        private static string Proof(ProvenBound? proven) =>
            proven == null ? "-" : proven.AllInputs ? "all inputs" : proven.UpTo < 0 ? "none" : $"up to {proven.UpTo}";

        private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
