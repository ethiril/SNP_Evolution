using SnpEvolution.Application;

namespace SnpEvolution.Tests.Application
{
    public class RunAdvisorTests
    {
        private static Settings ForSequence(params int[] values) =>
            new Settings { Target = new OutputTarget(TargetKind.Sequence, values), Task = Catalog.TargetTask };

        private static Suggestion? For(Advice advice, string setting) => advice.Suggestions.FirstOrDefault(suggestion => suggestion.Setting == setting);

        [Fact]
        public void SuggestsFixingAFibonacciTypo()
        {
            Settings settings = ForSequence(Sequences.FibonacciWithTypo.Take(13).ToArray());

            Suggestion? typo = For(RunAdvisor.Advise(settings), "Target");
            typo!.Apply(settings);

            Assert.Equal(144, settings.Target.Values[11]);
        }

        [Fact]
        public void SuggestsAUsefulPopulationAndIterativeEvolution()
        {
            Settings settings = ForSequence(1, 1, 2, 3, 5, 8);
            settings.PopulationSize = 4;
            settings.IterativeEvolution = false;

            Advice advice = RunAdvisor.Advise(settings);

            Assert.Equal(RunAdvisor.SuggestedPopulation.ToString(), For(advice, "Population size")!.Suggested);
            Assert.NotNull(For(advice, "Iterative evolution"));
        }

        [Fact]
        public void HasNothingMoreToSayOnceItsAdviceIsTaken()
        {
            Settings settings = ForSequence(Sequences.FibonacciWithTypo.Take(13).ToArray());
            settings.PopulationSize = 4;
            settings.MaxNeurons = 100;
            settings.StagnationRecovery = false;

            RunAdvisor.ApplyAll(settings, RunAdvisor.Advise(settings));

            Assert.Empty(RunAdvisor.Advise(settings).Suggestions);
        }

        [Fact]
        public void SuggestsModulesAndLexicaseOnlyForGrowingGaps()
        {
            Settings growing = ForSequence(1, 1, 2, 3, 5, 8, 13, 21);
            Settings steady = ForSequence(2, 3, 2, 3, 2, 3);

            Suggestion? modules = For(RunAdvisor.Advise(growing), "Modules and lexicase");
            modules!.Apply(growing);

            Assert.True(growing.Modules && growing.Lexicase);
            Assert.Null(For(RunAdvisor.Advise(steady), "Modules and lexicase"));
        }

        [Fact]
        public void AdvisingDoesNotChangeTheSettings()
        {
            Settings settings = ForSequence(1, 1, 2, 3, 5, 8, 13, 21);
            settings.PopulationSize = 4;

            RunAdvisor.Advise(settings);

            Assert.Equal(4, settings.PopulationSize);
        }
    }
}
