using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Modules;
using SnpEvolution.Evolution.Operators;

namespace SnpEvolution.Evolution.Search
{
    // Registering here is all a new search needs, since callers pick searches by type rather than by name.
    public static class SearchCatalog
    {
        public const int Elitism = 1;

        // The default: it evolves structure as well as rules, and has done best from scratch so far.
        public static readonly EvolutionSearch StructuralDefault = new EvolutionSearch("MAP-Elites over network size", context =>
            new MapElites(context.PopulationSize, context.Random, context.Conformed(context.CreateStartingNetwork), context.Evaluator, context.Conformed(new NeuronCrossover()), context.Conformed(context.StructuralMutation(1)),
                context.Lexicase ? new LexicaseSelection() : null));

        public static readonly CompositionSearch CompositionMapElites = new CompositionSearch("Composition search, MAP-Elites", (context, space) =>
            new MapElites(context.PopulationSize, context.Random, context.Conformed(space.NewNetwork), context.Evaluator, context.Conformed(new KeepFirstParent()), context.Conformed(space.Mutation(1, context.Pressure)),
                context.Lexicase ? new LexicaseSelection() : null));

        // What compose runs unless told otherwise.
        public static readonly CompositionSearch CompositionTournament = new CompositionSearch("Composition search, tournament of 3", (context, space) =>
            new GeneticAlgorithm(context.PopulationSize, context.Random, context.Conformed(space.NewNetwork), context.Evaluator,
                new GeneticOperators(context.Selection(new TournamentSelection(3)), context.Conformed(new KeepFirstParent()), context.Conformed(space.Mutation(Math.Max(context.MutationRate, 0.5f), context.Pressure))),
                Elitism, context.Log));

        public static readonly ShrinkSearch Shrink = new ShrinkSearch();

        private static readonly object Lock = new object();

        private static List<ISearch> searches = new List<ISearch>
        {
            new EvolutionSearch("Generational, roulette wheel (rule expressions only)", context =>
                Generational(context, new RouletteWheelSelection(), new RuleExpressionCrossover(), new RuleExpressionMutation(context.MutationRate, context.Factory.NextExpression)), evolvesRulesOnly: true),
            new EvolutionSearch("Generational, tournament of 3 (rule expressions only)", context =>
                Generational(context, new TournamentSelection(3), new RuleExpressionCrossover(), new RuleExpressionMutation(context.MutationRate, context.Factory.NextExpression)), evolvesRulesOnly: true),
            new EvolutionSearch("Generational, tournament of 3, structural", context =>
                Generational(context, context.Selection(new TournamentSelection(3)), new NeuronCrossover(), context.StructuralMutation(Math.Max(context.MutationRate, 0.5f)))),
            new EvolutionSearch("(mu + lambda) evolution strategy", context =>
                new MuPlusLambdaStrategy(Math.Max(1, context.PopulationSize / 5), context.PopulationSize, context.Random, context.Conformed(context.CreateStartingNetwork), context.Evaluator, context.Conformed(context.StructuralMutation(1)))),
            StructuralDefault,
            new EvolutionSearch("NEAT-style speciated", context =>
                new SpeciatedAlgorithm(context.PopulationSize, context.Random, context.Conformed(context.CreateStartingNetwork), context.Evaluator, context.Conformed(new NeuronCrossover()), context.Conformed(context.StructuralMutation(1)))),
            CompositionMapElites,
            CompositionTournament,
            Shrink,
            new PartSearch(),
            new ProgramSearch(),
        };

        public static IReadOnlyList<ISearch> All
        {
            get
            {
                lock (Lock)
                {
                    return searches;
                }
            }
        }

        // Searches that need seeds are left out, since a benchmark starts from nothing.
        public static IReadOnlyList<ISearch<Individual>> FromScratch => All.OfType<ISearch<Individual>>().Where(search => !search.NeedsSeeds).ToList();

        public static IReadOnlyList<EvolutionSearch> Evolution => All.OfType<EvolutionSearch>().ToList();

        public static void Register(ISearch search)
        {
            lock (Lock)
            {
                if (searches.Any(existing => existing.Name == search.Name))
                {
                    throw new ArgumentException($"A search named {search.Name} is already registered.", nameof(search));
                }
                searches = searches.Append(search).ToList();
            }
        }

        private static GeneticAlgorithm Generational(EvolutionContext context, IParentSelection selection, ICrossover crossover, IMutation mutation) =>
            new GeneticAlgorithm(context.PopulationSize, context.Random, context.Conformed(context.CreateStartingNetwork), context.Evaluator,
                new GeneticOperators(selection, context.Conformed(crossover), context.Conformed(mutation)), Elitism, context.Log);
    }
}
