using System;

namespace SnpEvolution.Specs.Tasks
{
    // Partial credit for a number that is wrong but close, which gives the search a slope towards the right one.
    public static class CloseCredit
    {
        public const float AtBest = 0.5f;

        public static float Score(int actual, int expected) => actual == expected ? 1 : AtBest / (1 + Math.Abs(actual - expected));
    }
}
