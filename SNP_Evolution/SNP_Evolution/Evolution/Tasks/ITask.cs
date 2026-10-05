using System.Collections.Generic;
using SnpEvolution.Simulation;

namespace SnpEvolution.Evolution.Tasks
{
    // One input to try a network on, and what to read back.
    public sealed record TaskCase(InputSpikes Input, Readout Readout);

    // What a network should do: the cases it is run on and how its results are scored. Generating a set, computing
    // a function and accepting a set are all tasks, so the algorithms and engines never need to know which it is.
    public interface ITask
    {
        string Name { get; }

        // How many input neurons a network for this task needs.
        int InputCount { get; }

        IReadOnlyList<TaskCase> Cases { get; }

        // The fewest simulation steps a run needs for the task to be solvable; runs are lengthened to at least this.
        int StepsNeeded => 0;

        // Scores one result per case, in case order, from 0 (useless) to 1 (solved).
        float Score(IReadOnlyList<TrialResult> results);

        // A short human-readable account of the results, for progress output.
        string Describe(IReadOnlyList<TrialResult> results);

        // Where the results fall in a grid of behaviours, for algorithms that keep the best network of each kind of
        // behaviour rather than only the fittest overall. Null when the task has no such grid.
        (int, int)? Niche(IReadOnlyList<TrialResult> results) => null;
    }

    // A task whose target is a list that can be cut short, such as a sequence of intervals or a binary word, so it
    // can be learned a few values at a time.
    public interface IPrefixTask : ITask
    {
        // How many values the whole target has.
        int Length { get; }

        // The same task with only the first length values of the target.
        ITask Prefix(int length);
    }
}
