using SnpEvolution.Compilation;
using SnpEvolution.Evolution.Accounting;
using SnpEvolution.Evolution.Fitness;
using SnpEvolution.Evolution.Genome;
using SnpEvolution.Evolution.Parts;
using SnpEvolution.Evolution.Search;
using SnpEvolution.Evolution.Tasks;
using SnpEvolution.Networks;
using SnpEvolution.Simulation;

namespace SnpEvolution.Tests.Compilation
{
    public class CompilerTests
    {
        private static readonly int[] Fibonacci = { 1, 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144, 233, 377, 610, 987, 1597, 2584 };

        private static FitnessEvaluator SequenceEvaluator(IReadOnlyList<int> values) =>
            new FitnessEvaluator(new SequentialCpuEngine(), new SequenceTask("target", values), new SimulationOptions(50, 2, OutputTiming.Interval), 1, new Random(1), new EvaluationBudget());

        private static IReadOnlyList<int> Generated(Network network, int maxSteps = 400) =>
            new ExhaustiveCpuEngine().CollectOutputs(new[] { network }, new SimulationOptions(maxSteps, 20, OutputTiming.Interval), new Random(1))[0];

        [Fact]
        public void FitsFibonacciAsTheSumOfTheTwoValuesBefore()
        {
            Recurrence? recurrence = Recurrence.Fit(Fibonacci.Take(16).ToList());

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
            Recurrence recurrence = Recurrence.Fit(Fibonacci.Take(16).ToList())!;
            Network network = RecurrenceCompiler.Compile(recurrence);

            FitnessResult result = SequenceEvaluator(Fibonacci).Evaluate(network);

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

        [Fact]
        public void ProgramsReadAndWriteTheSameWay()
        {
            RegisterProgram program = RegisterProgram.Parse("0: ADD r1 -> 1 | 2\n1: SUB r1 -> 2 else 3\n2: ADD r0 -> 3\n3: HALT\n");

            Assert.Equal(program.ToString(), RegisterProgram.Parse(program.ToString()).ToString());
            Assert.Equal(2, program.RegisterCount);
        }

        [Fact]
        public void ProgramsGenerateEveryNumberAlongEveryChoice()
        {
            RegisterProgram evens = RegisterProgram.Parse("ADD r0 -> 1\nADD r0 -> 0 | 2\nHALT");

            (IReadOnlyList<int> outputs, bool complete, _) = evens.Generate(maxSteps: 20);

            Assert.Equal(new[] { 2, 4, 6, 8, 10, 12 }, outputs.Take(6));
            Assert.False(complete);
        }

        [Fact]
        public void TheOutputRegisterCannotBeSubtractedFrom()
        {
            Assert.NotNull(RegisterProgram.Parse("SUB r0 -> 1 else 1\nHALT").Problem());
        }

        [Theory]
        [InlineData("ADD r0 -> 1\nADD r0 -> 2\nADD r0 -> 3\nHALT", new[] { 3 })]
        [InlineData("ADD r0 -> 1\nADD r0 -> 2 | 3\nADD r0 -> 3\nHALT", new[] { 2, 3 })]
        // Counts 2 or 3 into r1, then moves it into r0 one at a time.
        [InlineData("ADD r1 -> 1\nADD r1 -> 2 | 3\nADD r1 -> 3\nSUB r1 -> 4 else 5\nADD r0 -> 3\nHALT", new[] { 2, 3 })]
        // Two registers subtracted from by different instructions, with a choice inside the loop.
        [InlineData("ADD r1 -> 1\nADD r2 -> 2 | 3\nADD r2 -> 3\nSUB r2 -> 4 else 5\nADD r0 -> 3\nSUB r1 -> 6 else 7\nADD r0 -> 5\nHALT", new[] { 2, 3 })]
        public void CompiledProgramsGenerateWhatTheProgramDoes(string text, int[] expected)
        {
            RegisterProgram program = RegisterProgram.Parse(text);
            Assert.Equal(expected, program.Generate().Outputs);

            Assert.Equal(expected, Generated(RegisterMachineCompiler.Compile(program)));
        }

        // Outputs a, then (a, b) becomes (b, a + b): every Fibonacci number, with r4 only there to make the choice.
        private const string FibonacciProgram = @"
            0: ADD r1 -> 1
            1: ADD r2 -> 2
            2: ADD r4 -> 3 | 5
            3: SUB r1 -> 4 else 12
            4: ADD r0 -> 3
            5: SUB r1 -> 6 else 7
            6: ADD r3 -> 5
            7: SUB r2 -> 8 else 10
            8: ADD r1 -> 9
            9: ADD r3 -> 7
            10: SUB r3 -> 11 else 2
            11: ADD r2 -> 10
            12: HALT";

        [Fact]
        public void ACompiledFibonacciProgramGeneratesTheSameNumbersAsTheProgram()
        {
            RegisterProgram program = RegisterProgram.Parse(FibonacciProgram);
            (IReadOnlyList<int> outputs, _, int steps) = program.Generate(outputLimit: 21, valueLimit: 100);

            IReadOnlyList<int> generated = Generated(RegisterMachineCompiler.Compile(program), 4 * (steps + 2) + 31);

            Assert.Equal(new[] { 1, 2, 3, 5, 8, 13, 21 }, outputs);
            Assert.Equal(outputs, generated.Where(number => number <= 21));
        }

        [Fact]
        public void ProgramSearchFindsASmallSetAndItsNetworkGeneratesIt()
        {
            var search = new ProgramSearch(new ProgramSearchSettings(Population: 60));
            var target = new GeneratorTask("{2,3}", new[] { 2, 3 }, new JaccardFitness(new[] { 2, 3 }));
            var budget = new EvaluationBudget();

            SearchOutcome<RegisterProgram> best = search.Run(new SearchRequest<RegisterProgram>(target, budget, new Random(1), _ => { }) { MaxGenerations = 400 });

            Assert.True(best.Solved, best.Best!.ToString());
            Assert.Equal(new[] { 2, 3 }, Generated(RegisterMachineCompiler.Compile(best.Best!)));
            Assert.True(budget[EvaluationKind.InterpreterRun] > 0);
        }

        [Fact]
        public void ProgramSearchLearnsLargeSetsInStagesThatEndPastTheLargestNumber()
        {
            Assert.Equal(new long[] { 5, 13, 34, 89, 233, 610, 1976 }, ProgramScoring.StageBounds(new[] { 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144, 233, 377, 610, 987 }));
            Assert.Equal(new long[] { 14 }, ProgramScoring.StageBounds(new[] { 2, 4, 6 }));
        }

        [Fact]
        public void AStageChecksEveryTargetNumberUpToItsBoundAndNothingExtra()
        {
            var scoring = new ProgramScoring(new[] { 1, 2, 3, 5, 8 }, 2_000, new EvaluationBudget());

            ScoredProgram scored = scoring.Score(RegisterProgram.Parse("ADD r0 -> 1\nADD r0 -> 0 | 2\nHALT"));

            Assert.Equal(5, scored.Checks.Count);
        }

        [Fact]
        public void AnEditNeverLeavesAProgramWithoutInstructions()
        {
            var edits = new ProgramEdits(16, 4, new Random(1));
            RegisterProgram single = RegisterProgram.Parse("HALT");

            Assert.All(Enumerable.Range(0, 300), _ => Assert.NotEmpty(edits.Mutate(single).Instructions));
        }

        // Only the first numbers are generated before the limit on register 0 cuts the computations off, and that
        // still counts as complete, since nothing at or below the limit is lost.
        [Fact]
        public void TheOutputLimitLosesNothingBelowIt()
        {
            RegisterProgram evens = RegisterProgram.Parse("ADD r0 -> 1\nADD r0 -> 0 | 2\nHALT");

            (IReadOnlyList<int> outputs, bool complete, _) = evens.Generate(outputLimit: 7);

            Assert.Equal(new[] { 2, 4, 6 }, outputs);
            Assert.True(complete);
        }

        [Fact]
        [Slow]
        public void ShrinkingKeepsTheNetworkCorrectAndNeverGrowsIt()
        {
            int[] values = { 1, 2, 4, 8, 16, 32 };
            Network compiled = RecurrenceCompiler.Compile(Recurrence.Fit(values)!);
            FitnessEvaluator evaluator = SequenceEvaluator(values);
            var random = new Random(5);
            var space = new GenomeSpace(RuleForm: RuleForm.Standard, MaxNeurons: compiled.Neurons.Count, MaxInitialSpikes: 8, MaxProduce: 2);
            var factory = new NetworkFactory(space, new ExpressionGenerator(ExpressionGenerator.SimpleTemplates, 4, random), random);
            var setup = new NetworkSetup(20, 0.5f, factory, () => compiled, new NetworkScoring(() => new SequentialCpuEngine(), new SimulationOptions(50, 2, OutputTiming.Interval), 1));

            SearchOutcome<Individual> shrink = SearchCatalog.Shrink.Run(new SearchRequest<Individual>(evaluator.Task, new EvaluationBudget(), random, _ => { })
            {
                Seeds = new[] { new Individual(compiled) },
                MaxGenerations = 30,
                Networks = setup,
            });

            Assert.Equal(30, shrink.Generations);
            Assert.True(HardwareCost.Of(shrink.Best!.Genes).CompareTo(HardwareCost.Of(compiled)) <= 0);
            Assert.True(evaluator.ConfirmSolved(shrink.Best!.Genes).Solved);
        }
    }
}
