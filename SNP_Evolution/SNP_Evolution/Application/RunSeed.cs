using System;

namespace SnpEvolution.Application
{
    // A run's random source: a given seed makes the run repeatable, and without one every run differs. evolve-parts and
    // compose write files later runs build on, so they default to seed 1 and the same command writes the same files.
    internal static class RunSeed
    {
        public const int Repeatable = 1;

        public static Random For(int? seed) => seed is int given ? new Random(given) : new Random();
    }
}
