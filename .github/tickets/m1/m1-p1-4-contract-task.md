---
number: 10
title: Score parts against a contract with a ContractTask
milestone: M1
labels:
  - area: tasks
parent: m1-p1-contracts-ports-and-readouts
---

The algorithms only optimise `ITask`, so a contract needs a task before anything can evolve a part for it.
We want `ContractTask : ITask` that builds one `TaskCase` per contract case using the port input encodings and the port readout, and scores the four contract rules.

Its responsibilities are:
* Binding ports to neurons: input ports to the network's input neurons in contract order, out ports and done ports to fixed neuron positions declared by a `PortBinding` the task fixes (for evolution, the first neurons after the inputs)
* `Checks`: one check per case and rule, so lexicase and the stagnation tools see which rule fails where, and `CheckName` naming both
* `Score`: the mean of the checks, with partial credit for a count or interval that is close, as `FunctionTask` gives close outputs
* `Describe`: one line per failing check
* `StepsNeeded` from the contract's maximum latency plus the input length

Rule 2 should fail if done fires twice or never, because a done that fires twice restarts whatever is wired to it.
Rule 3 should compare every neuron's final spike count to its initial count, because a part that leaves a spike behind works once and then breaks inside a loop.
The task should report `Niche` from (neurons used, latency) so MAP-Elites can shrink parts without a new algorithm.

Where: new `SNP_Evolution/Evolution/Tasks/ContractTask.cs`; model the shape on `SNP_Evolution/Evolution/Tasks/FunctionTask.cs`. Tests in `SNP_Evolution.Tests/Evolution/TaskTests.cs` or a new `ContractTaskTests.cs`.

Done when:
- [ ] A network that never fires done scores 0 on rules 2 and 4 and full marks on rule 1
- [ ] Check names read like `n=3: done once`
- [ ] The task runs under the existing GA and MAP-Elites with no change to them
- [ ] `dotnet test` green

Read first: Claude Doc "Composing SN P Modules into Machines", section Ports and the start/done contract and Build plan step 1
