using System.Collections.Generic;
using SnpEvolution.Evolution.Contracts;

namespace SnpEvolution.Evolution.Tasks
{
    // What some tasks can do beyond what every task does. A caller asks for the ability it needs, never for a kind of task.

    // A small task of its own around a check, such as a few gaps of a sequence starting near it, to evolve a part that
    // does it on the side. Null when there is no such part for the check.
    public interface IFocusable : ITask
    {
        ITask? Focus(int check);
    }

    // Like a focus, but the part waits for a spike on its one input before it starts, so it can be chained after what a
    // network already does instead of running beside it from the first step.
    public interface ITriggerable : ITask
    {
        ITask? Triggered(int check);
    }

    // A part a stalled composition run could evolve to pass the checks no network passes, or null when there is none.
    public interface IProposing : ITask
    {
        Contract? Propose(IReadOnlyList<int> unsolvedChecks);
    }

    // A task whose target is a sequence of gaps between output spikes, whose shape can suggest the parts that make it.
    public interface ISequenceTask : ITask
    {
        IReadOnlyList<int> Expected { get; }
    }

    // A task whose target is a set of numbers to generate, so a search over other kinds of generator, such as register
    // programs, can be aimed at it.
    public interface ISetTask : ITask
    {
        IReadOnlyCollection<int> ExpectedSet { get; }

        float Score(IReadOnlyList<int> outputs);

        // Whether each expected number is among the outputs.
        IReadOnlyList<float> Checks(IReadOnlyList<int> outputs);
    }

    // A task whose target is a contract on named ports: a network for it can be verified, kept as a part, and composed
    // from parts wired to its boundary. ContractTask.Of gives the task a verifier runs.
    public interface IContractTask : ITask
    {
        Contract Contract { get; }

        PortBinding Binding { get; }

        // The contract's ports where a network for it has them, so composition search can wire parts to them by type.
        IReadOnlyList<PartPort> Boundary { get; }
    }
}
