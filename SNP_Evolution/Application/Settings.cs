using System.Collections.Generic;
using SnpEvolution.Search;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Benchmarking;
using SnpEvolution.Search.Genome;
using SnpEvolution.Search.Modules;
using SnpEvolution.Search.Proposals;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Application
{
    public sealed class Settings
    {
        public const int Elitism = SearchCatalog.Elitism;
        public const int MaxSpikeGroupSize = 4;

        public int MaxSteps { get; set; } = 50;
        public int Repetitions { get; set; } = 50;
        public int PopulationSize { get; set; } = 50;
        public float MutationRate { get; set; } = 0.1f;
        public int MaxGenerations { get; set; } = 125;

        // Network evaluations a run may spend in all, side runs and retests included; 0 for no limit but the generations.
        public long MaxEvaluations { get; set; }

        public EvaluationBudget RunBudget() => new EvaluationBudget(MaxEvaluations > 0 ? MaxEvaluations : null);
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

        // Keep every rule to threshold-and-reset forms, so evolved networks export to NIR (see Model.HardwareProfile).
        public bool HardwareProfile { get; set; }

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

        // Let every other side run build a part that waits for a trigger from the host, and evolve networks given a
        // new module copy apart for this many generations before they join the main run (0 to send them in at once).
        public bool TriggeredModules { get; set; } = true;
        public int ModuleIncubation { get; set; } = 30;
        public IReadOnlyList<string> ModuleFiles { get; set; } = System.Array.Empty<string>();
        // Where evolve-parts saves verified parts and later runs load them from, and the evaluations it may spend
        // searching for each part.
        public string PartLibraryFolder { get; set; } = DefaultPartLibraryFolder();
        public long PartBudget { get; set; } = 50_000;

        // How composition search mixes its edits, and how many part copies a network may hold.
        public CompositionMix Composition { get; set; } = new CompositionMix();

        public bool ProposeParts { get; set; } = true;
        public long ProposalBudget { get; set; } = 20_000;

        // Off by default, since the library should be one the runs found.
        public bool HandBuiltParts { get; set; }

        // Leaving the add loop out is the control for whether reusing it helps.
        public bool HandBuiltAddLoop { get; set; } = true;

        public int BenchmarkSeeds { get; set; } = 5;
        public long EvaluationBudget { get; set; } = 5_000;
        public int BenchmarkPopulationSize { get; set; } = 40;
        public CatalogEntry<Settings, BenchmarkTask> Task { get; set; } = Catalog.Tasks[0];
        public CatalogEntry<Settings, ISimulationEngine> Engine { get; set; } = Catalog.Engines[0];
        public CatalogEntry<Settings, IFitnessFunction> FitnessFunction { get; set; } = Catalog.FitnessFunctions[0];
        public EvolutionSearch Algorithm { get; set; } = Catalog.StructuralDefault;

        public SimulationOptions SimulationOptions => new SimulationOptions(MaxSteps, Repetitions, OutputTiming);

        public IReadOnlyList<string> MutationTemplates =>
            ExperimentalRules ? ExpressionGenerator.ExperimentalTemplates : ExpressionGenerator.SimpleTemplates;

        // Makes the task the one to evolve for; a suite task brings the rule form and timing it is meant for.
        public void UseTask(CatalogEntry<Settings, BenchmarkTask> task)
        {
            Task = task;
            if (task != Catalog.TargetTask)
            {
                BenchmarkTask chosen = task.Create(this);
                (RuleForm, OutputTiming) = (chosen.RuleForm, chosen.Timing);
            }
        }

        // The selected task, built with the rule form and output timing chosen here.
        public BenchmarkTask SelectedTask => Task.Create(this) with { RuleForm = RuleForm, Timing = OutputTiming };

        public GenomeSpace GenomeSpace(int inputCount) =>
            new GenomeSpace(InputCount: inputCount, RuleForm: RuleForm, MaxNeurons: MaxNeurons, MaxDelay: MaxDelay, MaxInitialSpikes: MaxInitialSpikes, MaxProduce: MaxProduce,
                DuplicateNeurons: DuplicateNeurons, HardwareProfile: HardwareProfile);

        // The stages for an iterative run, automatic unless a start length or step has been set.
        public CurriculumPlan CurriculumFor(IPrefixTask task)
        {
            CurriculumPlan automatic = task.Curriculum;
            return new CurriculumPlan(
                IterativeStartLength > 0 ? IterativeStartLength : automatic.StartLength,
                IterativeStep > 0 ? IterativeStep : automatic.Step);
        }

        public StagnationPolicy StagnationPolicy => new StagnationPolicy(Patience: StagnationPatience);

        // The modular loop reacts to a stall before stagnation recovery does.
        public ModulePolicy ModulePolicy => new ModulePolicy(
            Patience: System.Math.Max(5, StagnationPatience / 2), Triggered: TriggeredModules, IncubationGenerations: ModuleIncubation);

        // Proposals react to a stall at the same point, so a part is asked for before the population is shaken up.
        public ProposalPolicy ProposalPolicy => new ProposalPolicy(Patience: ModulePolicy.Patience, MaxProposals: ProposeParts ? new ProposalPolicy().MaxProposals : 0);

        public BenchmarkSettings BenchmarkSettings => new BenchmarkSettings(
            BenchmarkSeeds, EvaluationBudget, BenchmarkPopulationSize, MutationRate, MaxSteps, Repetitions,
            GenomeSpace(0), MutationTemplates, MaxSpikeGroupSize, () => Engine.Create(this));

        // A copy to try changes on without touching these settings.
        public Settings Copy() => (Settings)MemberwiseClone();

        // parts/ at the root of the repository the program runs in, or in the working directory outside one.
        public static string DefaultPartLibraryFolder() => System.IO.Path.Combine(RunFolders.WorkingRoot(), "parts");

        public static Settings Defaults() => new Settings
        {
            PopulationSize = 50,
            MaxGenerations = 25,
            Target = OutputTarget.Set(new[] { 2, 4, 6, 8, 10, 12, 14, 16 }),
        };
    }
}
