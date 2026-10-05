using System.Collections.Generic;
using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Benchmarking;
using SnpEvolution.Evolution.Modules;
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
        public int PopulationSize { get; set; } = 50;
        public float MutationRate { get; set; } = 0.1f;
        public int MaxGenerations { get; set; } = 125;
        public int SolvedRetestCount { get; set; } = 5;
        public OutputTarget Target { get; set; } = OutputTarget.Set(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });
        public bool ExperimentalRules { get; set; } = true;
        public RuleForm RuleForm { get; set; } = RuleForm.Legacy;
        public OutputTiming OutputTiming { get; set; } = OutputTiming.Legacy;
        public int MaxNeurons { get; set; } = 7;
        public int MaxDelay { get; set; } = 1;
        public int MaxProduce { get; set; } = 2;
        public int MaxInitialSpikes { get; set; } = MaxSpikeGroupSize;
        public bool DuplicateNeurons { get; set; }

        // Evolve sequences and binary words a few values at a time. Zero start length or step means automatic.
        public bool IterativeEvolution { get; set; } = true;
        public int IterativeStartLength { get; set; }
        public int IterativeStep { get; set; }

        // React when the best fitness stops improving for StagnationPatience generations.
        public bool StagnationRecovery { get; set; } = true;
        public int StagnationPatience { get; set; } = 50;

        // Pick parents by lexicase selection over the task's checks.
        public bool Lexicase { get; set; }

        // Build networks from modules found during the run, kept frozen unless FreezeModules is off. ModuleFiles are
        // saved networks to start the library with; without them every module is found by the run itself.
        public bool Modules { get; set; }
        public bool FreezeModules { get; set; } = true;
        public IReadOnlyList<string> ModuleFiles { get; set; } = System.Array.Empty<string>();
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
            new GenomeSpace(InputCount: inputCount, RuleForm: RuleForm, MaxNeurons: MaxNeurons, MaxDelay: MaxDelay, MaxInitialSpikes: MaxInitialSpikes, MaxProduce: MaxProduce,
                DuplicateNeurons: DuplicateNeurons);

        // The stages for an iterative run, automatic unless a start length or step has been set.
        public CurriculumPlan CurriculumFor(IPrefixTask task)
        {
            CurriculumPlan automatic = CurriculumPlan.For(task);
            return new CurriculumPlan(
                IterativeStartLength > 0 ? IterativeStartLength : automatic.StartLength,
                IterativeStep > 0 ? IterativeStep : automatic.Step);
        }

        public StagnationPolicy StagnationPolicy => new StagnationPolicy(Patience: StagnationPatience);

        // The modular loop reacts to a stall before stagnation recovery does.
        public ModulePolicy ModulePolicy => new ModulePolicy(Patience: System.Math.Max(5, StagnationPatience / 2));

        public BenchmarkSettings BenchmarkSettings => new BenchmarkSettings(
            BenchmarkSeeds, EvaluationBudget, BenchmarkPopulationSize, MutationRate, MaxSteps, Repetitions,
            GenomeSpace(0), MutationTemplates, MaxSpikeGroupSize, () => Engine.Create(this));

        // A copy to try changes on without touching these settings.
        public Settings Copy() => (Settings)MemberwiseClone();

        public static Settings Defaults() => new Settings
        {
            PopulationSize = 50,
            MaxGenerations = 25,
            Target = OutputTarget.Set(new[] { 2, 4, 6, 8, 10, 12, 14, 16 }),
        };
    }
}
