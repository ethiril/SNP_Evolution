using System.Collections.Generic;
using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Cli
{
    internal sealed class Settings
    {
        public const int Elitism = AlgorithmCatalog.Elitism;
        public const int MaxSpikeGroupSize = 4;

        public int MaxSteps { get; set; } = 50;
        public int Repetitions { get; set; } = 50;
        public int PopulationSize { get; set; } = 4;
        public float MutationRate { get; set; } = 0.1f;
        public int MaxGenerations { get; set; } = 125;
        public int SolvedRetestCount { get; set; } = 5;
        public OutputTarget Target { get; set; } = OutputTarget.Set(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
        public bool ExperimentalRules { get; set; } = true;
        public RuleForm RuleForm { get; set; } = RuleForm.Legacy;
        public OutputTiming OutputTiming { get; set; } = OutputTiming.Legacy;
        public int MaxNeurons { get; set; } = 7;
        public int BenchmarkSeeds { get; set; } = 5;
        public long EvaluationBudget { get; set; } = 5_000;
        public int BenchmarkPopulationSize { get; set; } = 40;
        public CatalogEntry<Settings, BenchmarkTask> Task { get; set; } = Catalog.Tasks[0];
        public CatalogEntry<Settings, ISimulationEngine> Engine { get; set; } = Catalog.Engines[0];
        public CatalogEntry<Settings, IFitnessFunction> FitnessFunction { get; set; } = Catalog.FitnessFunctions[0];
        public CatalogEntry<EvolutionContext, IGeneticAlgorithm> Algorithm { get; set; } = Catalog.StructuralDefault;

        public SimulationOptions SimulationOptions => new SimulationOptions(MaxSteps, Repetitions, OutputTiming);

        public IReadOnlyList<string> MutationTemplates =>
            ExperimentalRules ? ExpressionGenerator.ExperimentalTemplates : ExpressionGenerator.SimpleTemplates;

        // The selected task, built with the rule form and output timing chosen here.
        public BenchmarkTask SelectedTask => Task.Create(this) with { RuleForm = RuleForm, Timing = OutputTiming };

        public GenomeSpace GenomeSpace(int inputCount) =>
            new GenomeSpace(InputCount: inputCount, RuleForm: RuleForm, MaxNeurons: MaxNeurons, MaxInitialSpikes: MaxSpikeGroupSize);

        public BenchmarkSettings BenchmarkSettings => new BenchmarkSettings(
            BenchmarkSeeds, EvaluationBudget, BenchmarkPopulationSize, MutationRate, MaxSteps, Repetitions,
            GenomeSpace(0), MutationTemplates, MaxSpikeGroupSize, () => Engine.Create(this));

        public static Settings Defaults() => new Settings
        {
            PopulationSize = 10,
            MaxGenerations = 25,
            Target = OutputTarget.Set(new[] { 2, 4, 6, 8, 10, 12, 14, 16 }),
        };
    }
}
