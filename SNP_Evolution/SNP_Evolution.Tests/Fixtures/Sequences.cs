namespace SnpEvolution.Tests.Fixtures
{
    internal static class Sequences
    {
        public static readonly int[] Fibonacci = { 1, 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144, 233, 377, 610, 987, 1597, 2584 };

        // Index 11 reads 114 instead of 144, the slip the run advisor is meant to catch.
        public static readonly int[] FibonacciWithTypo = { 1, 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 114, 233, 377, 610, 987, 1597, 2584 };
    }
}
