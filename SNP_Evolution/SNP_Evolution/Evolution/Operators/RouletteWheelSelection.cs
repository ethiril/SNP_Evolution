using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Fitness;

namespace SnpEvolution.Evolution.Operators
{
    // Picks parents in proportion to fitness, falling back to the top tenth when rounding leaves the wheel unspent.
    public sealed class RouletteWheelSelection : IParentSelection
    {
        public Func<Individual> Prepare(IReadOnlyList<Individual> ranked, Random random)
        {
            float fitnessSum = ranked.Sum(individual => individual.Fitness);
            return () =>
            {
                double remaining = random.NextDouble() * fitnessSum;
                foreach (Individual individual in ranked)
                {
                    if (remaining < individual.Fitness)
                    {
                        return individual;
                    }
                    remaining -= individual.Fitness;
                }
                return ranked[random.Next(0, ranked.Count / 10 + 1)];
            };
        }
    }
}
