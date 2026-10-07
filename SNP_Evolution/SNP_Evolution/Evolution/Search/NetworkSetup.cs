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
    // How networks are scored: on a fresh engine of the given kind, with the given options, and retested this many
    // times before a sampled solve counts.
    public sealed record NetworkScoring(Func<ISimulationEngine> CreateEngine, SimulationOptions Options, int SolvedRetests)
    {
        public FitnessEvaluator Evaluator(ITask task, EvaluationBudget budget, Random random, EvaluationSource source = EvaluationSource.Main) =>
            new FitnessEvaluator(CreateEngine(), task, Options, SolvedRetests, random, budget, source);
    }

    // How a network search makes, edits and scores networks. Lexicase makes parents be picked by lexicase selection
    // where the search picks parents; Parts and Composition are the library and mix composition search builds from.
    public sealed record NetworkSetup(int PopulationSize, float MutationRate, NetworkFactory Factory, Func<Network> CreateStartingNetwork, NetworkScoring Scoring)
    {
        public bool Lexicase { get; init; }

        public ModuleLibrary? Parts { get; init; }

        public CompositionMix? Composition { get; init; }

        public EvolutionContext Context(IPopulationEvaluator evaluator, Random random, Action<string> log, MutationPressure? pressure = null, ModuleSupport? modules = null) =>
            new EvolutionContext(PopulationSize, MutationRate, random, CreateStartingNetwork, evaluator, Factory, log, pressure, Lexicase, modules, Parts, Composition);
    }
}
