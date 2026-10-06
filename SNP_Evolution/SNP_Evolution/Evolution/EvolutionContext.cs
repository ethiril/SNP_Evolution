using System;
using System.Collections.Generic;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution
{
    // Everything an algorithm needs to start evolving, independent of which algorithm it is. Lexicase makes parents
    // be picked by lexicase selection where the algorithm picks parents; Modules lets mutation use a module library.
    public sealed record EvolutionContext(
        int PopulationSize,
        float MutationRate,
        Random Random,
        Func<Network> CreateStartingNetwork,
        IPopulationEvaluator Evaluator,
        NetworkFactory Factory,
        Action<string> Log,
        MutationPressure? Pressure = null,
        bool Lexicase = false,
        ModuleSupport? Modules = null,
        ModuleLibrary? Parts = null,
        CompositionMix? Composition = null)
    {
        public WeightedMutation StructuralMutation(float rate) => WeightedMutation.Structural(rate, Factory, Pressure, Modules);

        // Under the hardware profile, what an operator or the starting networks make is put back within it, so no edit
        // has to know the profile; outside it they are returned as given.
        public IMutation Conformed(IMutation mutation) => Factory.Space.HardwareProfile ? new ProfileMutation(mutation) : mutation;

        public ICrossover Conformed(ICrossover crossover) => Factory.Space.HardwareProfile ? new ProfileCrossover(crossover) : crossover;

        public Func<Network> Conformed(Func<Network> create) => Factory.Space.HardwareProfile ? () => HardwareProfile.Conform(create()) : create;

        public IParentSelection Selection(IParentSelection usual) => Lexicase ? new LexicaseSelection(usual) : usual;
    }

    public sealed record AlgorithmChoice(string Name, Func<EvolutionContext, IGeneticAlgorithm> Create);

    // The algorithms on offer. A new one only needs to implement IGeneticAlgorithm and be listed here.
    public static class AlgorithmCatalog
    {
        public const int Elitism = 1;

        public static readonly IReadOnlyList<AlgorithmChoice> All = new[]
        {
            new AlgorithmChoice("Generational, roulette wheel (rule expressions only)", context =>
                Generational(context, new RouletteWheelSelection(), new RuleExpressionCrossover(), new RuleExpressionMutation(context.MutationRate, context.Factory.NextExpression))),
            new AlgorithmChoice("Generational, tournament of 3 (rule expressions only)", context =>
                Generational(context, new TournamentSelection(3), new RuleExpressionCrossover(), new RuleExpressionMutation(context.MutationRate, context.Factory.NextExpression))),
            new AlgorithmChoice("Generational, tournament of 3, structural", context =>
                Generational(context, context.Selection(new TournamentSelection(3)), new NeuronCrossover(), context.StructuralMutation(Math.Max(context.MutationRate, 0.5f)))),
            new AlgorithmChoice("(mu + lambda) evolution strategy", context =>
                new MuPlusLambdaStrategy(Math.Max(1, context.PopulationSize / 5), context.PopulationSize, context.Random, context.Conformed(context.CreateStartingNetwork), context.Evaluator, context.Conformed(context.StructuralMutation(1)))),
            new AlgorithmChoice("MAP-Elites over network size", context =>
                new MapElites(context.PopulationSize, context.Random, context.Conformed(context.CreateStartingNetwork), context.Evaluator, context.Conformed(new NeuronCrossover()), context.Conformed(context.StructuralMutation(1)),
                    context.Lexicase ? new LexicaseSelection() : null)),
            new AlgorithmChoice("NEAT-style speciated", context =>
                new SpeciatedAlgorithm(context.PopulationSize, context.Random, context.Conformed(context.CreateStartingNetwork), context.Evaluator, context.Conformed(new NeuronCrossover()), context.Conformed(context.StructuralMutation(1)))),
            new AlgorithmChoice(CompositionPrefix + "MAP-Elites", context =>
            {
                CompositionSpace space = CompositionSpace.For(context);
                return new MapElites(context.PopulationSize, context.Random, context.Conformed(space.NewNetwork), context.Evaluator, context.Conformed(new KeepFirstParent()), context.Conformed(space.Mutation(1, context.Pressure)),
                    context.Lexicase ? new LexicaseSelection() : null);
            }),
            new AlgorithmChoice(CompositionPrefix + "tournament of 3", context =>
            {
                CompositionSpace space = CompositionSpace.For(context);
                return new GeneticAlgorithm(context.PopulationSize, context.Random, context.Conformed(space.NewNetwork), context.Evaluator,
                    new GeneticOperators(context.Selection(new TournamentSelection(3)), context.Conformed(new KeepFirstParent()), context.Conformed(space.Mutation(Math.Max(context.MutationRate, 0.5f), context.Pressure))),
                    Elitism, context.Log);
            }),
        };

        // Composition search builds every network from library parts and glue, whatever starting network the run was given.
        public const string CompositionPrefix = "Composition search, ";

        public static bool IsComposition(string algorithmName) => algorithmName.StartsWith(CompositionPrefix);

        private static GeneticAlgorithm Generational(EvolutionContext context, IParentSelection selection, ICrossover crossover, IMutation mutation) =>
            new GeneticAlgorithm(context.PopulationSize, context.Random, context.Conformed(context.CreateStartingNetwork), context.Evaluator,
                new GeneticOperators(selection, context.Conformed(crossover), context.Conformed(mutation)), Elitism, context.Log);
    }
}
