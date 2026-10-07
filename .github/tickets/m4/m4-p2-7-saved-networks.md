---
number: 146
title: Superoptimise a saved network on its task, with held-out values
milestone: M4
labels:
  - area: search
  - area: cli
parent: m4-p2-superoptimisation
---

Runs of `evolve`, `compose` and `compile` save correct networks for tasks with no contract (Fibonacci, the natural numbers), and shrinking them is scored only on the values the task checks, so a smaller network can be right on those and wrong just past them.
We want `superopt --network FILE --task NAME` to superoptimise a saved network on its task, admitting a network only when it passes the task's values and a set of held-out values the search never scored.

Its responsibilities are:
* Loading the network and refusing it, with the first wrong value, when it does not solve the task
* Held-out values: for sequence and generator tasks, the values after the last one scored; for other tasks, inputs the task does not check
* The search: the same superoptimiser as for parts, with the task's check and the held-out values as admission
* Output: the smallest and the fastest admitted networks, saved to a run folder as the compile run's files are

Held-out values should never be scored during the search, because a search that sees them overfits to them as it does to the training values.
A task with no held-out values should be refused with a message saying so, because an unchecked shrink is what this ticket exists to stop.
The run should report how many candidates passed training but failed held-out values, because that is the overfitting rate the spec route is meant to remove.

Where: `Search/ShrinkSearch.cs`, `Application/CompileService.cs`, `Application/NetworkRunService.cs`, `Application/RunFolders.cs`, `Storage/NetworkFiles.cs`, `Specs/Tasks/SequenceTask.cs`, `Specs/Tasks/GeneratorTask.cs`, `Search/Benchmarking/TaskSuite.cs`, the superoptimiser from this epic.

Done when:
- [ ] The compiled Fibonacci network from a `compile` run shrinks to at most 10 neurons and gives the next 4 values past the ones it was scored on
- [ ] A network that fails its task is refused with the first wrong value
- [ ] The run reports how many candidates failed only the held-out values
- [ ] `dotnet test` green

Read first: RESEARCH.md "Spec to verified circuit", README "Compile, then shrink"
