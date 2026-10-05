---
number: 25
title: Build the Fibonacci register program from parts
milestone: M1
labels:
  - area: modules
parent: m1-p4-fibonacci-by-hand
---

The representation needs to be shown able to express a growing sequence before search is trusted with it.
We want a test-only builder that assembles the Fibonacci register program from library parts (registers, fan-out, the count-to-interval timer, a sequencer for the swap) and glue neurons, using the typed wiring, with the round-timing fix decided beforehand.

The builder should live in the test project, not the app, because a Fibonacci-specific network in the app would be a seed by another name.
The test should run the existing Fibonacci `SequenceTask` on the result, because that is the task the flat runs failed at and the comparison has to be like for like.

Where: new `SNP_Evolution.Tests/Evolution/FibonacciCompositionTests.cs`; `SNP_Evolution/Evolution/Tasks/SequenceTask.cs` and `TaskSuite.cs` for the target.

Done when:
- [ ] At least 15 gaps match exactly on the exhaustive engine
- [ ] Neurons, synapses and rules are printed and recorded in RESEARCH.md as the size target
- [ ] `dotnet test` green

Read first: Claude Doc "Composing SN P Modules into Machines", Build plan step 4
