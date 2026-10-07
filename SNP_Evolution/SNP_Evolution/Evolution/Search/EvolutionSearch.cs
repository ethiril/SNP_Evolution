using System;
using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Algorithms;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Search
{
    // A genetic algorithm behind ISearch: each run makes the algorithm from the request's network setup, scores on the
    // task against the budget, and runs the generation loop until the best network is confirmed solved or the budget,
    // the generations or the run give out. Seeds become the starting networks. A stall policy wraps the algorithm in
    // stagnation recovery, with newcomers made the way this search makes networks.
    public class EvolutionSearch : ISearch<Individual>
    {
        private readonly Func<EvolutionContext, IGeneticAlgorithm> create;

        public EvolutionSearch(string name, Func<EvolutionContext, IGeneticAlgorithm> create, bool evolvesRulesOnly = false)
        {
            Name = name;
            this.create = create;
            EvolvesRulesOnly = evolvesRulesOnly;
        }

        public string Name { get; }

        // Rule-only algorithms keep the starting network's structure, so they cannot build a network from scratch.
        public bool EvolvesRulesOnly { get; }

        // The algorithm alone, for a run that steps it a generation at a time itself.
        public IGeneticAlgorithm Create(EvolutionContext context) => create(context);

        // Recovery for a stalled run: newcomers half new networks, half heavily mutated copies of the best. A search
        // that makes networks its own way makes the newcomers that way instead.
        public virtual StagnationRecovery Recovering(IGeneticAlgorithm algorithm, EvolutionContext context, StagnationPolicy policy, MutationPressure pressure,
            Func<Network> newNetwork, IMutation heavyMutation) =>
            new StagnationRecovery(algorithm, policy, context.PopulationSize, pressure, newNetwork, heavyMutation, context.Random, context.Log);

        public SearchOutcome<Individual> Run(SearchRequest<Individual> request)
        {
            NetworkSetup setup = request.RequireNetworks();
            FitnessEvaluator evaluator = setup.Scoring.Evaluator(request.Task, request.Budget, request.Random);
            MutationPressure? pressure = request.Stall != null ? new MutationPressure() : null;
            EvolutionContext context = setup.Context(evaluator, request.Random, request.Log, pressure);
            if (request.Seeds.Count > 0)
            {
                context = context with { CreateStartingNetwork = () => request.Seeds[request.Random.Next(request.Seeds.Count)].Genes };
            }
            IGeneticAlgorithm algorithm = Create(context);
            if (request.Stall is StagnationPolicy policy)
            {
                algorithm = Recovering(algorithm, context, policy, pressure!, context.CreateStartingNetwork, WeightedMutation.Structural(1, context.Factory));
            }
            (SearchStop stop, _) = GenerationLoop.Run(algorithm, request.MaxGenerations, () => request.Budget.IsSpent,
                run => run.Best is Individual best && SolveCheck.Confirms(best, evaluator), request.Cancellation);
            return Outcome(stop, algorithm.Best, request.Budget);
        }

        public static SearchOutcome<Individual> Outcome(SearchStop stop, Individual? best, EvaluationBudget budget) =>
            new SearchOutcome<Individual>(stop, best, best?.Fitness ?? 0, best?.Description ?? "", budget.Report());
    }

    // Builds every network from library parts and glue, whatever starting network the run was given, and makes the
    // newcomers of a stalled run as compositions too.
    public sealed class CompositionSearch : EvolutionSearch
    {
        public CompositionSearch(string name, Func<EvolutionContext, CompositionSpace, IGeneticAlgorithm> create)
            : base(name, context => create(context, CompositionSpace.For(context)))
        {
        }

        public override StagnationRecovery Recovering(IGeneticAlgorithm algorithm, EvolutionContext context, StagnationPolicy policy, MutationPressure pressure,
            Func<Network> newNetwork, IMutation heavyMutation)
        {
            CompositionSpace space = CompositionSpace.For(context);
            return base.Recovering(algorithm, context, policy, pressure, space.NewNetwork, space.Mutation(1));
        }
    }
}
