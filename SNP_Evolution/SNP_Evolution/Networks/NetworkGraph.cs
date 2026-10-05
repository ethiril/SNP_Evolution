using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;

namespace SnpEvolution.Networks
{
    // Draws a network as an SN P system diagram in standalone SVG: each neuron a rounded box holding its spikes and
    // rules, each synapse an arrow, and arrows to and from the environment on the output and input neurons.
    // Neurons are laid out in columns by their distance from the inputs, so the flow of spikes reads left to right.
    public static class NetworkGraph
    {
        private const double CharWidth = 7.3;
        private const double LineHeight = 16;
        private const double Padding = 10;
        private const double ColumnGap = 90;
        private const double RowGap = 36;
        private const double Margin = 60;
        private const double LoopHeight = 28;
        private const double SideRoom = 40;

        // A neuron's box, with the left and right edges of the column it sits in, which synapses route around.
        private sealed record Box(double X, double Y, double Width, double Height, int Column, double ColumnLeft, double ColumnRight)
        {
            public double CentreX => X + Width / 2;
            public double CentreY => Y + Height / 2;
            public double Right => X + Width;
            public double Bottom => Y + Height;
        }

        public static string Svg(Network network)
        {
            int count = network.Neurons.Count;
            List<string>[] labels = network.Neurons.Select(Lines).ToArray();
            int[] columns = Columns(network);
            var boxes = new Box[count];

            // Size each box to its text, then stack each column's boxes and centre the columns on one another.
            double[] widths = labels.Select(lines => lines.Max(line => line.Length) * CharWidth + 2 * Padding).ToArray();
            double[] heights = labels.Select(lines => lines.Count * LineHeight + 2 * Padding).ToArray();
            int columnCount = columns.Max() + 1;
            var members = Enumerable.Range(0, columnCount).Select(column => Enumerable.Range(0, count).Where(index => columns[index] == column).ToList()).ToList();
            double[] columnHeights = members.Select(column => column.Sum(index => heights[index]) + RowGap * Math.Max(0, column.Count - 1)).ToArray();
            double tallest = columnHeights.Max();
            double x = Margin;
            for (int column = 0; column < columnCount; column++)
            {
                double y = Margin + LoopHeight * 2 + (tallest - columnHeights[column]) / 2;
                double columnWidth = members[column].Count == 0 ? 0 : members[column].Max(index => widths[index]);
                foreach (int index in members[column])
                {
                    boxes[index] = new Box(x + (columnWidth - widths[index]) / 2, y, widths[index], heights[index], column, x, x + columnWidth);
                    y += heights[index] + RowGap;
                }
                x += columnWidth + ColumnGap;
            }

            double width = x - ColumnGap + Margin + SideRoom;
            double height = Margin * 2 + LoopHeight * 4 + tallest;
            var svg = new StringBuilder();
            svg.AppendLine(Invariant($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width:0}\" height=\"{height:0}\" viewBox=\"0 0 {width:0} {height:0}\" font-family=\"Menlo, Consolas, monospace\" font-size=\"12\">"));
            svg.AppendLine("<defs><marker id=\"arrow\" viewBox=\"0 0 10 10\" refX=\"10\" refY=\"5\" markerWidth=\"7\" markerHeight=\"7\" orient=\"auto-start-reverse\"><path d=\"M0,0 L10,5 L0,10 z\" fill=\"#444\"/></marker></defs>");
            svg.AppendLine("<rect width=\"100%\" height=\"100%\" fill=\"white\"/>");

            double top = boxes.Min(box => box.Y);
            int lane = 0;
            for (int source = 0; source < count; source++)
            {
                foreach (int target in network.Neurons[source].Connections.Select(position => position - 1).Where(index => index >= 0 && index < count))
                {
                    svg.AppendLine(Invariant($"<path d=\"{Edge(boxes[source], boxes[target], top, ref lane)}\" fill=\"none\" stroke=\"#444\" stroke-width=\"1.3\" stroke-linejoin=\"round\" marker-end=\"url(#arrow)\"/>"));
                }
            }
            for (int index = 0; index < count; index++)
            {
                Neuron neuron = network.Neurons[index];
                Box box = boxes[index];
                double inY = box.Y + box.Height * 0.3, outY = box.Y + box.Height * 0.7;
                if (neuron.IsInput)
                {
                    svg.AppendLine(Invariant($"<path d=\"M{box.X - 40:0.#},{inY:0.#} L{box.X:0.#},{inY:0.#}\" stroke=\"#2b6cb0\" stroke-width=\"1.6\" marker-end=\"url(#arrow)\"/>"));
                    svg.AppendLine(Invariant($"<text x=\"{box.X - 44:0.#}\" y=\"{inY + 4:0.#}\" text-anchor=\"end\" fill=\"#2b6cb0\">in</text>"));
                }
                if (neuron.IsOutput)
                {
                    svg.AppendLine(Invariant($"<path d=\"M{box.Right:0.#},{outY:0.#} L{box.Right + 40:0.#},{outY:0.#}\" stroke=\"#c05621\" stroke-width=\"1.6\" marker-end=\"url(#arrow)\"/>"));
                    svg.AppendLine(Invariant($"<text x=\"{box.Right + 44:0.#}\" y=\"{outY + 4:0.#}\" fill=\"#c05621\">env</text>"));
                }
                string stroke = neuron.IsOutput ? "#c05621" : neuron.IsInput ? "#2b6cb0" : "#333";
                string fill = neuron.Module is ModuleTag tag ? ModuleFill(tag.Module) : "#fafafa";
                svg.AppendLine(Invariant($"<rect x=\"{box.X:0.#}\" y=\"{box.Y:0.#}\" width=\"{box.Width:0.#}\" height=\"{box.Height:0.#}\" rx=\"14\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"{(neuron.IsOutput || neuron.IsInput ? 2 : 1.3)}\"/>"));
                List<string> lines = labels[index];
                for (int line = 0; line < lines.Count; line++)
                {
                    double baseline = box.Y + Padding + LineHeight * (line + 1) - 4;
                    string weight = line == 0 ? " font-weight=\"bold\"" : "";
                    svg.AppendLine(Invariant($"<text x=\"{box.CentreX:0.#}\" y=\"{baseline:0.#}\" text-anchor=\"middle\"{weight}>{SecurityElement.Escape(lines[line])}</text>"));
                }
            }
            svg.AppendLine("</svg>");
            return svg.ToString();
        }

        // The neuron's name and roles, its initial spikes when it has any, then one rule per line.
        private static List<string> Lines(Neuron neuron, int index)
        {
            var lines = new List<string>
            {
                "n" + (index + 1) + (neuron.IsInput ? " (in)" : "") + (neuron.IsOutput ? " (out)" : "") + (neuron.Module is ModuleTag tag ? $" [m{tag.Module}]" : ""),
            };
            if (neuron.InitialSpikes > 0)
            {
                lines.Add(NetworkNotation.Spikes(neuron.InitialSpikes));
            }
            lines.AddRange(neuron.Rules.Select(NetworkNotation.Rule));
            return lines;
        }

        // Breadth-first distance from the input neurons, or from neurons nothing sends to when there are no inputs.
        // Anything still unreached starts a new search of its own, so every neuron gets a column.
        internal static int[] Columns(Network network)
        {
            int count = network.Neurons.Count;
            var columns = Enumerable.Repeat(-1, count).ToArray();
            var incoming = new bool[count];
            foreach (int target in network.Neurons.SelectMany(neuron => neuron.Connections).Where(position => position >= 1 && position <= count))
            {
                incoming[target - 1] = true;
            }
            var roots = Enumerable.Range(0, count).Where(index => network.Neurons[index].IsInput).ToList();
            if (roots.Count == 0)
            {
                roots = Enumerable.Range(0, count).Where(index => !incoming[index]).ToList();
            }
            while (true)
            {
                var queue = new Queue<int>(roots.Where(index => columns[index] < 0));
                foreach (int root in queue)
                {
                    columns[root] = 0;
                }
                while (queue.Count > 0)
                {
                    int index = queue.Dequeue();
                    foreach (int target in network.Neurons[index].Connections.Select(position => position - 1).Where(target => target >= 0 && target < count && columns[target] < 0))
                    {
                        columns[target] = columns[index] + 1;
                        queue.Enqueue(target);
                    }
                }
                int unreached = Array.IndexOf(columns, -1);
                if (unreached < 0)
                {
                    return columns;
                }
                roots = new List<int> { unreached };
            }
        }

        // Forward synapses curve from right edge to left edge. Every other synapse leaves on the right and runs in a
        // lane through the gap beside the column, so it never crosses a neuron: back to the same column, or over the
        // top of the diagram and down the gap before an earlier column. Lanes are staggered to keep them apart, and a
        // self-synapse is a small loop.
        private static string Edge(Box from, Box to, double top, ref int lane)
        {
            if (ReferenceEquals(from, to))
            {
                double left = from.CentreX + 8, right = from.CentreX + 28, crest = from.Y - LoopHeight;
                return Invariant($"M{left:0.#},{from.Y:0.#} C{left:0.#},{crest:0.#} {right:0.#},{crest:0.#} {right:0.#},{from.Y:0.#}");
            }
            if (to.Column > from.Column)
            {
                double bend = (to.X - from.Right) / 2;
                return Invariant($"M{from.Right:0.#},{from.CentreY:0.#} C{from.Right + bend:0.#},{from.CentreY:0.#} {to.X - bend:0.#},{to.CentreY:0.#} {to.X:0.#},{to.CentreY:0.#}");
            }
            double spread = 6 * (lane++ % 6);
            double outward = from.ColumnRight + 14 + spread;
            if (to.Column == from.Column)
            {
                return Rounded((from.Right, from.CentreY), (outward, from.CentreY), (outward, to.CentreY), (to.Right, to.CentreY));
            }
            double peak = top - 16 - spread;
            double inward = to.ColumnLeft - 14 - spread;
            return Rounded((from.Right, from.CentreY), (outward, from.CentreY), (outward, peak), (inward, peak), (inward, to.CentreY), (to.X, to.CentreY));
        }

        // A path through the points with each corner rounded off, so lanes that turn in the same place stay distinct.
        private static string Rounded(params (double X, double Y)[] points)
        {
            const double Radius = 8;
            var path = new StringBuilder(Invariant($"M{points[0].X:0.#},{points[0].Y:0.#}"));
            for (int index = 1; index < points.Length - 1; index++)
            {
                (double X, double Y) corner = points[index];
                (double X, double Y) before = Toward(corner, points[index - 1], Radius);
                (double X, double Y) after = Toward(corner, points[index + 1], Radius);
                path.Append(Invariant($" L{before.X:0.#},{before.Y:0.#} Q{corner.X:0.#},{corner.Y:0.#} {after.X:0.#},{after.Y:0.#}"));
            }
            path.Append(Invariant($" L{points[^1].X:0.#},{points[^1].Y:0.#}"));
            return path.ToString();
        }

        // The point a distance from one point toward another, stopping halfway for a short segment.
        private static (double X, double Y) Toward((double X, double Y) from, (double X, double Y) to, double distance)
        {
            double length = Math.Sqrt((to.X - from.X) * (to.X - from.X) + (to.Y - from.Y) * (to.Y - from.Y));
            double step = length == 0 ? 0 : Math.Min(distance, length / 2) / length;
            return (from.X + (to.X - from.X) * step, from.Y + (to.Y - from.Y) * step);
        }

        private static readonly string[] ModuleFills = { "#e6f0ff", "#e9f7ec", "#fff4e0", "#f3e8ff", "#e0f7f7", "#fde8ec" };

        private static string ModuleFill(int module) => ModuleFills[((module % ModuleFills.Length) + ModuleFills.Length) % ModuleFills.Length];

        private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
    }
}
