using System.Collections.Generic;
using SnpEvolution.Evolution;
using SnpEvolution.Simulation;

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
        public CatalogEntry<Settings, ISimulationEngine> Engine { get; set; } = Catalog.Engines[0];
        public CatalogEntry<Settings, IFitnessFunction> FitnessFunction { get; set; } = Catalog.FitnessFunctions[0];
        public CatalogEntry<EvolutionRun, IGeneticAlgorithm> Algorithm { get; set; } = Catalog.Algorithms[0];

        public SimulationOptions SimulationOptions => new SimulationOptions(MaxSteps, Repetitions);

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
