using SnpEvolution.Compilation;
using SnpEvolution.Model;
using SnpEvolution.Search.Fitness;
using static SnpEvolution.Tests.Fixtures.Runs;

namespace SnpEvolution.Tests.Compilation
{
    public class RecurrenceCompilerTests
    {
        private const int FittedValues = 16;

        [Fact]
        public void FitsFibonacciAsTheSumOfTheTwoValuesBefore()
        {
            Recurrence? recurrence = Recurrence.Fit(Sequences.Fibonacci.Take(FittedValues).ToList());

            Assert.NotNull(recurrence);
            Assert.Equal(new[] { 1, 1 }, recurrence!.Coefficients);
            Assert.Equal(new[] { 1, 1 }, recurrence.Initial);
        }

        [Fact]
        public void FindsNoRecurrenceForASequenceWithoutOne()
        {
            Assert.Null(Recurrence.Fit(new[] { 1, 2, 1, 3, 1, 4, 1, 5 }));
        }

        // The network is fitted to 16 values and keeps going: the next two are right as well.
        [Fact]
        public void CompiledFibonacciMakesExactlyTheGapsAndContinuesPastTheTarget()
        {
            Recurrence recurrence = Recurrence.Fit(Sequences.Fibonacci.Take(FittedValues).ToList())!;
            Network network = RecurrenceCompiler.Compile(recurrence);

            FitnessResult result = SequenceEvaluator(Sequences.Fibonacci).Evaluate(network);

            Assert.Equal(1f, result.Fitness);
            Assert.Equal(15, network.Neurons.Count);
            Assert.All(network.Neurons, neuron => Assert.True(neuron.Rules.Count <= 3));
        }

        [Theory]
        [InlineData(new[] { 1, 2, 4, 8, 16, 32, 64 })]
        [InlineData(new[] { 1, 2, 5, 12, 29, 70, 169 })]
        [InlineData(new[] { 1, 1, 2, 4, 7, 13, 24, 44, 81 })]
        [InlineData(new[] { 3, 3, 3, 3, 3, 3 })]
        [InlineData(new[] { 2, 1, 3, 4, 7, 11, 18, 29 })]
        [InlineData(new[] { 5, 1, 1, 2, 3, 5, 8, 13 })]
        public void CompiledRecurrencesMakeTheirSequences(int[] values)
        {
            Recurrence recurrence = Recurrence.Fit(values)!;

            Assert.Equal(1f, SequenceEvaluator(values).Evaluate(RecurrenceCompiler.Compile(recurrence)).Fitness);
        }
    }
}
