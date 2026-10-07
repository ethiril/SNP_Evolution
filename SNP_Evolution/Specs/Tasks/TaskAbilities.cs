using System.Collections.Generic;
using SnpEvolution.Specs.Contracts;

namespace SnpEvolution.Specs.Tasks
{
    // What some tasks can do beyond what every task does. A caller asks for the ability it needs, never for a kind of task.

    // Null when there is no such part for the check.
    public interface IFocusable : ITask
    {
        ITask? Focus(int check);
    }

    // The part waits for a trigger so it can be chained after what a host already does.
    public interface ITriggerable : ITask
    {
        ITask? Triggered(int check);
    }

    // Null when there is no part to propose.
    public interface IProposing : ITask
    {
        Contract? Propose(IReadOnlyList<int> unsolvedChecks);
    }

    // Its shape can suggest the parts that make it.
    public interface ISequenceTask : ITask
    {
        IReadOnlyList<int> Expected { get; }
    }

    // Lets a search over other generators, such as register programs, aim at the set.
    public interface ISetTask : ITask
    {
        IReadOnlyCollection<int> ExpectedSet { get; }

        float Score(IReadOnlyList<int> outputs);

        IReadOnlyList<float> Checks(IReadOnlyList<int> outputs);
    }

    // ContractTask.Of gives the task a verifier runs.
    public interface IContractTask : ITask
    {
        Contract Contract { get; }

        PortBinding Binding { get; }

        // The contract's ports where a network for it has them, so composition search can wire parts to them by type.
        IReadOnlyList<PartPort> Boundary { get; }
    }
}
