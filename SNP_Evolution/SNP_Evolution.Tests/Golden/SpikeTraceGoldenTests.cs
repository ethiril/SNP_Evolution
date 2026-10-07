using System.Text;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Verification;

namespace SnpEvolution.Tests.Golden
{
    // Held, sent and after counts per step, so a change to the step function the exporters emit shows in a golden file.
    public class SpikeTraceGoldenTests
    {
        [Fact]
        public void TheExportersStepRunsEveryPartAsBefore()
        {
            var text = new StringBuilder();
            foreach ((string name, Part part) in GoldenParts.All())
            {
                text.Append("== ").Append(name).Append('\n');
                if (SpikeTrace.Choices(part.Network).FirstOrDefault() is string choice)
                {
                    text.Append(choice).Append('\n');
                    continue;
                }
                foreach ((string label, InputSpikes input, int steps) in SpikeTrace.Cases(part.Contract))
                {
                    text.Append(label).Append('\n');
                    // held/sent/after for each neuron, sent shown as - when it applied no rule.
                    AppendFolded(text, SpikeTrace.Run(part.Network, input, steps).Steps
                        .Select(row => string.Join(' ', row.Select(neuron => $"{neuron.Held}/{(neuron.Applied ? neuron.Sent.ToString() : "-")}/{neuron.After}"))).ToList());
                }
            }
            GoldenFile.Check("exporter-steps", text.ToString());
        }

        // A run of identical steps is one first-last line, which keeps the file a size a reviewer can read.
        private static void AppendFolded(StringBuilder text, List<string> rows)
        {
            int first = 0;
            while (first < rows.Count)
            {
                int last = first;
                while (last + 1 < rows.Count && rows[last + 1] == rows[first])
                {
                    last++;
                }
                text.Append(first == last ? $"{first}" : $"{first}-{last}").Append(": ").Append(rows[first]).Append('\n');
                first = last + 1;
            }
        }
    }
}
