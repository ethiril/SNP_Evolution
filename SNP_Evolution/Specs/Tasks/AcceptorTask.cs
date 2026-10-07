using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Simulation;

namespace SnpEvolution.Specs.Tasks
{
    public sealed record AcceptorExample(int Number, bool Accept);

    // The network reads a number on its input neuron and accepts it by halting, as SN P acceptors do. Scored by
    // balanced accuracy, so rejecting or accepting everything only earns a half.
    public sealed class AcceptorTask : ITask
    {
        public AcceptorTask(string name, IReadOnlyList<AcceptorExample> examples)
        {
            Name = name;
            Examples = examples;
            Cases = examples.Select(example => new TaskCase(InputSpikes.Numbers(example.Number), Readout.Halting)).ToList();
        }

        public string Name { get; }

        public IReadOnlyList<AcceptorExample> Examples { get; }

        public int InputCount => 1;

        public IReadOnlyList<TaskCase> Cases { get; }

        public static AcceptorTask Of(string name, System.Func<int, bool> accepts, IEnumerable<int> numbers) =>
            new AcceptorTask(name, numbers.Select(number => new AcceptorExample(number, accepts(number))).ToList());

        public float Score(IReadOnlyList<TrialResult> results)
        {
            IReadOnlyList<float> checks = Checks(results);
            return TaskScoring.BalancedAccuracy(Enumerable.Range(0, Examples.Count), example => Examples[example].Accept, example => checks[example]);
        }

        public IReadOnlyList<float> Checks(IReadOnlyList<TrialResult> results) =>
            Examples.Select((example, index) => results[index].CanHalt == example.Accept ? 1f : 0f).ToList();

        public string CheckName(int check) => $"{(Examples[check].Accept ? "accept" : "reject")} {Examples[check].Number}";

        public string Describe(IReadOnlyList<TrialResult> results) =>
            "accepts {" + string.Join(",", Examples.Where((_, index) => results[index].CanHalt).Select(example => example.Number)) + "}";
    }
}
