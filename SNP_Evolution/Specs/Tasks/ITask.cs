using System.Collections.Generic;
using SnpEvolution.Model;
using SnpEvolution.Simulation;

namespace SnpEvolution.Specs.Tasks
{
    // One input to try a network on, and what to read back. Watch names the neurons a Ports readout watches.
    public sealed record TaskCase(InputSpikes Input, Readout Readout, PortWatch? Watch = null)
    {
        // The case as a trial of the network.
        public Trial Of(Network network) => new Trial(network, Input, Readout, Watch);
    }

    // What a network should do: the cases it is run on and how its results are scored. Generating a set, computing
    // a function and accepting a set are all tasks, so the algorithms and engines never need to know which it is.
    public interface ITask
    {
        string Name { get; }

        // How many input neurons a network for this task needs.
        int InputCount { get; }

        IReadOnlyList<TaskCase> Cases { get; }

        // Sampled runs vary, so most tasks count a network as solving them a little short of 1 (Solved).
        float SolvedFitness => Solved.Sampled;

        // The fewest simulation steps a run needs for the task to be solvable; runs are lengthened to at least this.
        int StepsNeeded => 0;

        // Scores one result per case, in case order, from 0 (useless) to 1 (solved).
        float Score(IReadOnlyList<TrialResult> results);

        // A short human-readable account of the results, for progress output.
        string Describe(IReadOnlyList<TrialResult> results);

        // Where the results fall in a grid of behaviours, for algorithms that keep the best network of each kind of
        // behaviour rather than only the fittest overall. Null when the task has no such grid.
        (int, int)? Niche(IReadOnlyList<TrialResult> results) => null;

        // The separate things the task checks, each scored from 0 to 1, such as each gap of a sequence or each
        // example of a function. They show which parts of the target a network gets right, so selection can keep
        // networks that are right about different parts, and a stalled run can see what nothing does yet. Empty when
        // the task cannot be split up.
        IReadOnlyList<float> Checks(IReadOnlyList<TrialResult> results) => System.Array.Empty<float>();

        // A short name for a check, for progress output.
        string CheckName(int check) => $"check {check + 1}";
    }

    // A task whose target is a list that can be cut short, such as a sequence of intervals or a binary word, so it
    // can be learned a few values at a time.
    public interface IPrefixTask : ITask
    {
        // How many values the whole target has.
        int Length { get; }

        // The same task with only the first length values of the target.
        ITask Prefix(int length);

        // Short targets make good first stages, a value at a time.
        CurriculumPlan Curriculum => new CurriculumPlan(System.Math.Min(3, Length), 1);
    }

    // How long the first stage's target is and how many values each later stage adds.
    public sealed record CurriculumPlan(int StartLength, int Step)
    {
        // Every stage's target length, ending with the whole target.
        public IReadOnlyList<int> Lengths(int total)
        {
            var lengths = new List<int>();
            for (int length = System.Math.Clamp(StartLength, 1, total); length < total; length += System.Math.Max(1, Step))
            {
                lengths.Add(length);
            }
            lengths.Add(total);
            return lengths;
        }
    }
}
