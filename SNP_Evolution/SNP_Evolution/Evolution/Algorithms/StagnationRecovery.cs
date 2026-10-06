using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Operators;
using SnpEvolution.Networks;

namespace SnpEvolution.Evolution.Algorithms
{
    // When to react to a stalled search, and how hard. Patience is the generations without improvement before each
    // reaction; ImmigrantFraction is the share of the population replaced when escalating; after MaxExtraEdits
    // escalations without progress the search restarts around its best network.
    public sealed record StagnationPolicy(int Patience = 50, double ImmigrantFraction = 0.25, int MaxExtraEdits = 3)
    {
        public static StagnationPolicy Default { get; } = new StagnationPolicy();
    }

    // Wraps an algorithm and watches its best fitness. Each time it stalls for the policy's patience, mutation
    // pressure rises by one extra edit per child and newcomers replace part of the population: half random networks,
    // half heavily mutated copies of the best. Once pressure is at its limit and still nothing improves, newcomers
    // replace everything but the best and pressure drops back to normal. Any improvement calms it down again.
    public sealed class StagnationRecovery : IGeneticAlgorithm
    {
        private const int HeavyEdits = 4;
        private const float ImprovementTolerance = 1e-6f;

        private readonly IGeneticAlgorithm inner;
        private readonly StagnationPolicy policy;
        private readonly int populationSize;
        private readonly MutationPressure pressure;
        private readonly Func<Network> createRandomNetwork;
        private readonly IMutation heavyMutation;
        private readonly Random random;
        private readonly Action<string> log;
        private float? bestFitness;
        private int stale;

        public StagnationRecovery(
            IGeneticAlgorithm inner,
            StagnationPolicy policy,
            int populationSize,
            MutationPressure pressure,
            Func<Network> createRandomNetwork,
            IMutation heavyMutation,
            Random random,
            Action<string>? log = null)
        {
            this.inner = inner;
            this.policy = policy;
            this.populationSize = populationSize;
            this.pressure = pressure;
            this.createRandomNetwork = createRandomNetwork;
            this.heavyMutation = heavyMutation;
            this.random = random;
            this.log = log ?? Console.WriteLine;
        }

        public IGeneticAlgorithm Inner => inner;

        public IReadOnlyList<Individual> Population => inner.Population;

        public int Generation => inner.Generation;

        public Individual? Best => inner.Best;

        public IReadOnlyList<IReadOnlyList<float>> FitnessHistory => inner.FitnessHistory;

        public int Escalations { get; private set; }

        public int Restarts { get; private set; }

        public void NextGeneration()
        {
            inner.NextGeneration();
            if (inner.Best is not Individual best || !GeneticAlgorithm.IsRecordableFitness(best.Fitness))
            {
                return;
            }
            if (bestFitness == null || best.Fitness > bestFitness + ImprovementTolerance)
            {
                bestFitness = best.Fitness;
                stale = 0;
                if (pressure.ExtraEdits > 0)
                {
                    pressure.ExtraEdits = 0;
                    log("Fitness improved, so mutation is back to normal.");
                }
                return;
            }
            if (++stale < policy.Patience)
            {
                return;
            }
            stale = 0;
            if (pressure.ExtraEdits >= policy.MaxExtraEdits)
            {
                Restart(best);
            }
            else
            {
                Escalate(best);
            }
        }

        public void Immigrate(IReadOnlyList<Network> newcomers) => inner.Immigrate(newcomers);

        // A new task is a fresh start, so the stall count and pressure go back to zero.
        public void Rescore()
        {
            inner.Rescore();
            bestFitness = null;
            stale = 0;
            pressure.ExtraEdits = 0;
        }

        private void Escalate(Individual best)
        {
            Escalations++;
            pressure.ExtraEdits++;
            int count = Math.Max(1, (int)Math.Round(populationSize * policy.ImmigrantFraction));
            inner.Immigrate(Newcomers(count, best));
            log($"No improvement in {policy.Patience} generations: {pressure.ExtraEdits} extra edit(s) per child, and {count} newcomers.");
        }

        private void Restart(Individual best)
        {
            Restarts++;
            pressure.ExtraEdits = 0;
            int count = Math.Max(1, populationSize - 1);
            inner.Immigrate(Newcomers(count, best));
            log($"Still stalled after {policy.MaxExtraEdits} escalations: restarting around the best network with {count} newcomers.");
        }

        private List<Network> Newcomers(int count, Individual best) =>
            Enumerable.Range(0, count).Select(index => index % 2 == 0 ? createRandomNetwork() : HeavilyMutated(best.Genes)).ToList();

        private Network HeavilyMutated(Network network)
        {
            for (int edit = 0; edit < HeavyEdits; edit++)
            {
                network = heavyMutation.Mutate(network, random);
            }
            return network;
        }
    }
}
