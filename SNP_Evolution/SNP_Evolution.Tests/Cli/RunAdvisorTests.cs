using SnpEvolution.Cli;
using SnpEvolution.Evolution.Tasks;

namespace SnpEvolution.Tests.Cli
{
    public class RunAdvisorTests
    {
        private static Settings ForSequence(params int[] values) =>
            new Settings { Target = new OutputTarget(TargetKind.Sequence, values), Task = Catalog.TargetTask };

        private static Suggestion? For(Advice advice, string setting) => advice.Suggestions.FirstOrDefault(suggestion => suggestion.Setting == setting);

        [Fact]
        public void SuggestsFixingAFibonacciTypo()
        {
            Settings settings = ForSequence(1, 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 114, 233);

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
            Settings settings = ForSequence(1, 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 114, 233);
            settings.PopulationSize = 4;
            settings.MaxNeurons = 100;
            settings.StagnationRecovery = false;

            RunAdvisor.ApplyAll(settings, RunAdvisor.Advise(settings));

            Assert.Empty(RunAdvisor.Advise(settings).Suggestions);
        }

        [Fact]
        public void AdvisingDoesNotChangeTheSettings()
        {
            Settings settings = ForSequence(1, 1, 2, 3, 5, 8, 13, 21);
            settings.PopulationSize = 4;

            RunAdvisor.Advise(settings);

            Assert.Equal(4, settings.PopulationSize);
        }

        [Fact]
        public void OnlyOrderedTargetsEvolveIteratively()
        {
            Settings sequence = ForSequence(1, 1, 2, 3, 5, 8);
            var set = new Settings { Target = OutputTarget.Set(new[] { 2, 4, 6, 8 }), Task = Catalog.TargetTask };

            Assert.True(EvolutionSession.IsIterative(sequence, sequence.SelectedTask, out _));
            Assert.False(EvolutionSession.IsIterative(set, set.SelectedTask, out _));
            sequence.IterativeEvolution = false;
            Assert.False(EvolutionSession.IsIterative(sequence, sequence.SelectedTask, out _));
        }
    }
}
