using System;
using SnpEvolution.Model;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Search.Genome;
using SnpEvolution.Search.Modules;
using SnpEvolution.Search.Operators;
using SnpEvolution.Simulation;
using SnpEvolution.Specs.Accounting;
using SnpEvolution.Specs.Parts;
using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Search
{
    public sealed record NetworkScoring(Func<ISimulationEngine> CreateEngine, SimulationOptions Options, int SolvedRetests)
    {
        public FitnessEvaluator Evaluator(ITask task, EvaluationBudget budget, Random random, EvaluationSource source = EvaluationSource.Main) =>
            new FitnessEvaluator(CreateEngine(), task, Options, SolvedRetests, random, budget, source);
    }

    public sealed record NetworkSetup(int PopulationSize, float MutationRate, NetworkFactory Factory, Func<Network> CreateStartingNetwork, NetworkScoring Scoring)
    {
        public bool Lexicase { get; init; }

        public ModuleLibrary? Parts { get; init; }

        public CompositionMix? Composition { get; init; }

        public EvolutionContext Context(IPopulationEvaluator evaluator, Random random, Action<string> log, MutationPressure? pressure = null, ModuleSupport? modules = null) =>
            new EvolutionContext(PopulationSize, MutationRate, random, CreateStartingNetwork, evaluator, Factory, log, pressure, Lexicase, modules, Parts, Composition);
    }
}
