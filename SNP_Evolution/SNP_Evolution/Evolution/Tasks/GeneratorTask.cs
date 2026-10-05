using System.Collections.Generic;
using System.Linq;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution.Tasks
{
    // The network runs with no input and should generate exactly the expected set, scored by a pluggable function.
    public sealed class GeneratorTask : ITask
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

        public float Score(IReadOnlyList<TrialResult> results) => fitness.Score(results[0].Outputs);

        // Whether each expected number is among the outputs.
        public IReadOnlyList<float> Checks(IReadOnlyList<TrialResult> results) =>
            ExpectedSet.Select(number => results[0].Outputs.Contains(number) ? 1f : 0f).ToList();

        public string CheckName(int check) => $"output {ExpectedSet.ElementAt(check)}";

        public string Describe(IReadOnlyList<TrialResult> results) => "{" + string.Join(", ", results[0].Outputs.Distinct()) + "}";
    }
}
