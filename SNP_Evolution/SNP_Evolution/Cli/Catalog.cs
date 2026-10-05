using System;
using System.Collections.Generic;
using SnpEvolution.Evolution;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Cli
{
    internal sealed record CatalogEntry<TContext, T>(string Name, Func<TContext, T> Create);

    // Everything an algorithm needs to start evolving, independent of which algorithm it is.
    internal sealed record EvolutionRun(
        Settings Settings,
        Random Random,
        Func<Network> CreateStartingNetwork,
        Func<string> CreateMutationExpression,
        IPopulationEvaluator Evaluator);

    // The swappable parts offered in the settings menu. A new engine, fitness function or algorithm only
    // needs to implement its interface and be listed here; the first entry of each list is the default.
    internal static class Catalog
    {
        public static readonly IReadOnlyList<CatalogEntry<Settings, ISimulationEngine>> Engines = new[]
        {
            new CatalogEntry<Settings, ISimulationEngine>("CPU, all cores", _ => new ParallelCpuEngine()),
            new CatalogEntry<Settings, ISimulationEngine>("CPU, single thread", _ => new SequentialCpuEngine()),
        };

        public static readonly IReadOnlyList<CatalogEntry<Settings, IFitnessFunction>> FitnessFunctions = new[]
        {
            new CatalogEntry<Settings, IFitnessFunction>("Set coverage (F1)", settings => new SetCoverageFitness(settings.ExpectedSet)),
            new CatalogEntry<Settings, IFitnessFunction>("Jaccard similarity", settings => new JaccardFitness(settings.ExpectedSet)),
        };

        public static readonly IReadOnlyList<CatalogEntry<EvolutionRun, IGeneticAlgorithm>> Algorithms = new[]
        {
            new CatalogEntry<EvolutionRun, IGeneticAlgorithm>("Generational, roulette wheel", run => Generational(run, new RouletteWheelSelection())),
            new CatalogEntry<EvolutionRun, IGeneticAlgorithm>("Generational, tournament of 3", run => Generational(run, new TournamentSelection(3))),
        };

        private static GeneticAlgorithm Generational(EvolutionRun run, IParentSelection selection) =>
            new GeneticAlgorithm(
                run.Settings.PopulationSize,
                run.Random,
                run.CreateStartingNetwork,
                run.Evaluator,
                new GeneticOperators(selection, new RuleExpressionCrossover(), new RuleExpressionMutation(run.Settings.MutationRate, run.CreateMutationExpression)),
                Settings.Elitism);
    }
}
