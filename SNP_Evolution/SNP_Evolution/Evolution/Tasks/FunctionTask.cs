using System;
using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution.Tasks
{
    public sealed record FunctionExample(IReadOnlyList<int> Arguments, int Result);

    // The network reads its arguments, each on its own input neuron as two spikes n steps apart, and should always
    // output the result. A wrong output still earns a little credit for being close, which gives the search a slope.
    public sealed class FunctionTask : ITask
    {
        public FunctionTask(string name, IReadOnlyList<FunctionExample> examples)
        {
            Name = name;
            Examples = examples;
            InputCount = examples.Max(example => example.Arguments.Count);
            Cases = examples.Select(example => new TaskCase(InputSpikes.Numbers(example.Arguments.ToArray()), Readout.Output)).ToList();
        }

        public string Name { get; }

        public IReadOnlyList<FunctionExample> Examples { get; }

        public int InputCount { get; }

        public IReadOnlyList<TaskCase> Cases { get; }

        public static FunctionTask Of(string name, Func<int, int> function, IEnumerable<int> arguments) =>
            new FunctionTask(name, arguments.Select(argument => new FunctionExample(new[] { argument }, function(argument))).ToList());

        public static FunctionTask Of(string name, Func<int, int, int> function, IEnumerable<(int, int)> arguments) =>
            new FunctionTask(name, arguments.Select(pair => new FunctionExample(new[] { pair.Item1, pair.Item2 }, function(pair.Item1, pair.Item2))).ToList());

        public float Score(IReadOnlyList<TrialResult> results) => Checks(results).Average();

        public string Describe(IReadOnlyList<TrialResult> results) =>
            string.Join("  ", Examples.Select((example, index) =>
                $"f({string.Join(",", example.Arguments)})={{{string.Join(",", results[index].Outputs.Distinct())}}}/{example.Result}"));

        public IReadOnlyList<float> Checks(IReadOnlyList<TrialResult> results) =>
            Examples.Select((example, index) => ScoreCase(results[index].Outputs, example.Result)).ToList();

        public string CheckName(int check) => $"f({string.Join(",", Examples[check].Arguments)})={Examples[check].Result}";

        private static float ScoreCase(IReadOnlyList<int> outputs, int expected) =>
            outputs.Count == 0 ? 0 : (float)outputs.Average(output => CloseCredit.Score(output, expected));
    }
}
