using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Fitness;

namespace SnpEvolution.Evolution.Operators
{
    // Draws a few individuals at random and keeps the fittest, which stays selective even when fitnesses are close.
    public sealed class TournamentSelection : IParentSelection
    {
        private readonly int size;

        public TournamentSelection(int size)
        {
            this.size = size;
        }

        // The population is ranked, so the lowest drawn index is the fittest contestant.
        public Func<Individual> Prepare(IReadOnlyList<Individual> ranked, Random random) =>
            () => ranked[Enumerable.Range(0, size).Min(_ => random.Next(ranked.Count))];
    }
}
