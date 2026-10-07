using System;
using SnpEvolution.Model;
using SnpEvolution.Search.Algorithms;
using SnpEvolution.Search.Fitness;
using SnpEvolution.Search.Operators;
using SnpEvolution.Specs.Accounting;

namespace SnpEvolution.Search
{
    // Seeds become the starting networks, and a stall policy wraps the algorithm in stagnation recovery.
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

        // For a run that steps the algorithm itself, with stages, modules and proposals around it.
        public IGeneticAlgorithm Create(EvolutionContext context) => create(context);

        // Overridden by searches that make networks their own way, so newcomers are made that way too.
        public virtual StagnationRecovery Recovering(IGeneticAlgorithm algorithm, EvolutionContext context, StagnationPolicy policy, MutationPressure pressure,
            Func<Network> newNetwork, IMutation heavyMutation) =>
            new StagnationRecovery(algorithm, policy, context.PopulationSize, pressure, newNetwork, heavyMutation, context.Random, context.Log);

        public SearchOutcome<Individual> Run(SearchRequest<Individual> request)
        {
            NetworkSetup setup = request.RequireNetworks();
            FitnessEvaluator evaluator = setup.Scoring.Evaluator(request.Task, request.Budget, request.Random);
            var pressure = new MutationPressure();
            EvolutionContext context = setup.Context(evaluator, request.Random, request.Log, pressure);
            if (request.Seeds.Count > 0)
            {
                context = context with { CreateStartingNetwork = () => request.Seeds[request.Random.Next(request.Seeds.Count)].Genes };
            }
            IGeneticAlgorithm algorithm = Create(context);
            if (request.Stall is StagnationPolicy policy)
            {
                algorithm = Recovering(algorithm, context, policy, pressure, context.CreateStartingNetwork, WeightedMutation.Structural(1, context.Factory));
            }
            (SearchStop stop, _) = GenerationLoop.Run(algorithm, request.MaxGenerations, () => request.Budget.IsSpent,
                run => run.Best is Individual best && SolveCheck.Confirms(best, evaluator), request.Cancellation);
            return Outcome(stop, algorithm.Best, request.Budget);
        }

        public static SearchOutcome<Individual> Outcome(SearchStop stop, Individual? best, EvaluationBudget budget) =>
            new SearchOutcome<Individual>(stop, best, best?.Fitness ?? 0, best?.Description ?? "", budget.Report());
    }

    // Newcomers of a stalled run are compositions too, so recovery never breaks a part open.
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
