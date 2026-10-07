using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Simulation;

namespace SnpEvolution.Specs.Tasks
{
    // The network runs with no input and should generate exactly the expected set, scored by a pluggable function.
    public sealed class GeneratorTask : ISetTask
    {
        private readonly IFitnessFunction fitness;

        public GeneratorTask(string name, IReadOnlyCollection<int> expectedSet, IFitnessFunction fitness)
        {
            Name = name;
            ExpectedSet = expectedSet;
            this.fitness = fitness;
        }

        public string Name { get; }

        public IReadOnlyCollection<int> ExpectedSet { get; }

        public int InputCount => 0;

        public IReadOnlyList<TaskCase> Cases { get; } = new[] { new TaskCase(InputSpikes.None, Readout.Output) };

        public float Score(IReadOnlyList<TrialResult> results) => Score(results[0].Outputs);

        public float Score(IReadOnlyList<int> outputs) => fitness.Score(outputs);

        public IReadOnlyList<float> Checks(IReadOnlyList<TrialResult> results) => Checks(results[0].Outputs);

        public IReadOnlyList<float> Checks(IReadOnlyList<int> outputs) => ExpectedSet.Select(number => outputs.Contains(number) ? 1f : 0f).ToList();

        public string CheckName(int check) => $"output {ExpectedSet.ElementAt(check)}";

        public string Describe(IReadOnlyList<TrialResult> results) => "{" + string.Join(", ", results[0].Outputs.Distinct()) + "}";
    }
}
