using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Evolution.Contracts;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution.Tasks
{
    // A task together with how to search for it: which rule form to build networks from and how outputs are timed.
    public sealed record BenchmarkTask(ITask Task, RuleForm RuleForm, OutputTiming Timing)
    {
        public string Name => Task.Name;
    }

    // Benchmark tasks with known small SN P solutions, from easy to hard, covering every kind of task.
    public static class TaskSuite
    {
        public static IReadOnlyList<BenchmarkTask> Generators { get; } = new[]
        {
            Generator("Generate {4}", new[] { 4 }),
            Generator("Generate {1..9}", Enumerable.Range(1, 9)),
            Generator("Generate evens {2..16}", Enumerable.Range(1, 8).Select(n => 2 * n)),
            Generator("Generate multiples of 3 {3..18}", Enumerable.Range(1, 6).Select(n => 3 * n)),
        };

        public static IReadOnlyList<BenchmarkTask> Functions { get; } = new[]
        {
            Standard(FunctionTask.Of("Compute n", n => n, Enumerable.Range(1, 6))),
            Standard(FunctionTask.Of("Compute n + 1", n => n + 1, Enumerable.Range(1, 6))),
            Standard(FunctionTask.Of("Compute 2n", n => 2 * n, Enumerable.Range(1, 6))),
            Standard(FunctionTask.Of("Compute n1 + n2", (first, second) => first + second,
                new[] { (1, 1), (1, 3), (2, 2), (3, 1), (2, 4), (4, 3) })),
        };

        public static IReadOnlyList<BenchmarkTask> Acceptors { get; } = new[]
        {
            Standard(AcceptorTask.Of("Accept n >= 4", n => n >= 4, Enumerable.Range(1, 10))),
            Standard(AcceptorTask.Of("Accept even n", n => n % 2 == 0, Enumerable.Range(1, 12))),
            Standard(AcceptorTask.Of("Accept multiples of 3", n => n % 3 == 0, Enumerable.Range(1, 12))),
        };

        // Controllers: a sensor train in, the output judged window by window, with no start or done.
        public static IReadOnlyList<BenchmarkTask> Streaming { get; } = new[]
        {
            Standard(StreamingTask.Debouncer(spikes: 2, within: 3)),
            Standard(StreamingTask.RateDetector(spikes: 3, within: 6)),
        };

        public static IReadOnlyList<BenchmarkTask> Contracts { get; } = ArithmeticParts.Contracts.Select(contract => Standard(new ContractTask(contract))).ToList();

        public static IReadOnlyList<BenchmarkTask> All { get; } = Generators.Concat(Functions).Concat(Acceptors).Concat(Streaming).Concat(Contracts).ToList();

        private static BenchmarkTask Generator(string name, IEnumerable<int> expected)
        {
            List<int> set = expected.ToList();
            return new BenchmarkTask(new GeneratorTask(name, set, new JaccardFitness(set)), RuleForm.Mixed, OutputTiming.Legacy);
        }

        private static BenchmarkTask Standard(ITask task) => new BenchmarkTask(task, RuleForm.Standard, OutputTiming.Interval);
    }
}
