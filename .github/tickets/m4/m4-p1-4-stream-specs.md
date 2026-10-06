---
number: 78
title: Write streaming tasks as specs
milestone: M4
labels:
  - area: tasks
parent: m4-p1-specs-as-targets
---

The streaming task builds its debouncer and rate detector by hand in its factory methods, so a new controller needs new code rather than a spec.
We want a stream kind of spec: a reference state machine over the input spike train that says, window by window, whether the output should be silent, fire once or be active, turned into a `StreamingTask`.

Its responsibilities are:
* A stream spec: a reference that reads the input train and gives the windows and their targets
* Input trains drawn from a seeded generator in the spec, with held-out trains that are longer or denser
* The existing debouncer and rate detector rewritten as stream specs that give the same tasks

The rewritten debouncer and rate detector should give exactly the windows they give now for the same seed, because saved runs and benchmarks were scored on them.
Held-out trains should run longer than training trains, because a controller that drifts or fills up shows it only after many windows.

Where: `SNP_Evolution/Evolution/Tasks/StreamingTask.cs`, `SNP_Evolution/Evolution/Tasks/SpikeTrains.cs`, `SNP_Evolution/Evolution/Tasks/TaskSuite.cs`; the spec type from this epic; tests in `SNP_Evolution.Tests/Evolution/StreamingTaskTests.cs`.

Done when:
- [ ] Debouncer and rate detector specs give the same windows as the current factory methods for seeds 1 to 4
- [ ] A held-out run of a stream spec is at least twice as long as its training runs
- [ ] `dotnet test` green

Read first: RESEARCH.md "Spec to verified circuit", "Use cases and a practical path" (item 7)
