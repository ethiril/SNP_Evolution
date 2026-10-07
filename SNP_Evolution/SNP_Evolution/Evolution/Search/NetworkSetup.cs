using System;
using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Genome;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution.Search
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
