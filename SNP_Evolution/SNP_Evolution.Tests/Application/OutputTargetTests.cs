using SnpEvolution.Application;
using SnpEvolution.Specs.Tasks;

namespace SnpEvolution.Tests.Application
{
    public class OutputTargetTests
    {
        [Theory]
        [InlineData("1,1,2,3,5,8,13", new[] { 1, 1, 2, 3, 5, 8, 13 })]
        [InlineData("1, 1, 2, 3, 5 ...", new[] { 1, 1, 2, 3, 5 })]
        [InlineData("{2 4 6}", new[] { 2, 4, 6 })]
        [InlineData("[3,1,4]…", new[] { 3, 1, 4 })]
        public void NumbersParseWithAnySeparatorsBracketsAndEllipsis(string input, int[] expected)
        {
            Assert.True(OutputTarget.TryParse(TargetKind.Sequence, input, out OutputTarget target));
            Assert.Equal(expected, target.Values);
        }

        [Theory]
        [InlineData("")]
        [InlineData("1,0,2")]
        [InlineData("1,-2")]
        [InlineData("1,x")]
        public void NumbersMustBePositiveIntegers(string input)
        {
            Assert.False(OutputTarget.TryParse(TargetKind.Set, input, out _));
        }

        [Fact]
        public void BinaryWordsIgnoreSpacingAndNeedAOne()
        {
            Assert.True(OutputTarget.TryParse(TargetKind.BinaryWord, "0110 1001_0", out OutputTarget target));
            Assert.Equal(new[] { 0, 1, 1, 0, 1, 0, 0, 1, 0 }, target.Values);
            Assert.False(OutputTarget.TryParse(TargetKind.BinaryWord, "0000", out _));
            Assert.False(OutputTarget.TryParse(TargetKind.BinaryWord, "0120", out _));
        }

        [Fact]
        public void EachKindMakesItsTask()
        {
            var fitness = new JaccardFitness(new[] { 1 });

            Assert.IsType<GeneratorTask>(new OutputTarget(TargetKind.Set, new[] { 1, 1, 2 }).CreateTask(fitness));
            Assert.IsType<SequenceTask>(new OutputTarget(TargetKind.Sequence, new[] { 1, 1, 2 }).CreateTask(fitness));
            Assert.IsType<SpikeWordTask>(new OutputTarget(TargetKind.BinaryWord, new[] { 0, 1 }).CreateTask(fitness));
        }
    }
}
