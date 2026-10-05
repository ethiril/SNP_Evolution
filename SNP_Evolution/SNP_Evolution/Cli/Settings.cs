using System.Collections.Generic;
using SnpEvolution.Evolution;

namespace SnpEvolution.Cli
{
    internal sealed class Settings
    {
        public const int Elitism = 1;
        public const int MaxSpikeGroupSize = 4;

        public int MaxSteps { get; set; } = 50;
        public int Repetitions { get; set; } = 50;
        public int PopulationSize { get; set; } = 4;
        public float MutationRate { get; set; } = 0.1f;
        public int MaxGenerations { get; set; } = 125;
        public int SolvedRetestCount { get; set; } = 5;
        public List<int> ExpectedSet { get; set; } = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        public bool ExperimentalRules { get; set; } = true;

        public IReadOnlyList<string> MutationTemplates =>
            ExperimentalRules ? ExpressionGenerator.ExperimentalTemplates : ExpressionGenerator.SimpleTemplates;

        public static Settings Defaults() => new Settings
        {
            PopulationSize = 10,
            MaxGenerations = 25,
            ExpectedSet = new List<int> { 2, 4, 6, 8, 10, 12, 14, 16 },
        };
    }
}
