using System;
using System.Collections.Generic;

namespace SnpEvolution.Evolution
{
    public sealed class ExpressionGenerator
    {
        public static readonly IReadOnlyList<string> SimpleTemplates = new[] { "x" };

        // 'x' and 'y' are placeholders, each replaced by its own random run of spikes.
        public static readonly IReadOnlyList<string> ExperimentalTemplates = new[] { "x", "x+", "x*", "x?", "x(y)+", "x(y)*", "x(y)?" };

        private readonly IReadOnlyList<string> templates;
        private readonly int maxSpikeGroupSize;
        private readonly Random random;

        public ExpressionGenerator(IReadOnlyList<string> templates, int maxSpikeGroupSize, Random random)
        {
            this.templates = templates;
            this.maxSpikeGroupSize = maxSpikeGroupSize;
            this.random = random;
        }

        public string Next()
        {
            string template = templates[random.Next(templates.Count)];
            string leadingSpikes = new string('a', random.Next(1, maxSpikeGroupSize));
            string groupedSpikes = new string('a', random.Next(1, maxSpikeGroupSize));
            return template.Replace("x", leadingSpikes).Replace("y", groupedSpikes);
        }
    }
}
