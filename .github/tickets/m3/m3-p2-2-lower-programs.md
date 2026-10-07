---
number: 62
title: Lower part programs to compositions
milestone: M3
labels:
  - area: modules
parent: m3-p2-programs-of-parts
---

A part program that solves in the interpreter is not yet a network, and promotion, verification and export all work on compositions.
We want a lowering from a part program to a `Composition`: one part copy per call, done-to-start wires for control flow, count wires for register traffic, and glue (or register parts) for registers that outlive a call.

Its responsibilities are:
* Control flow: a call's done port starts the next call; two done ports reaching one call go through a merge part, and parallel calls are joined with a join part, both from the library
* Data flow: a register written by one call and read by a later one is a register part, started when its reader starts
* Sequence output: the output instruction drives the task's output neuron through a count-to-interval part or the equivalent glue
* A check that the lowered network gives the interpreter's outputs on every case

The lowering should only use parts from the library and plain relays, because a hand-written glue circuit per construct would be the hand-built seed under another name.
Where the lowering needs a part the library lacks (a join, a merge, a timer), it should say which, because that is a proposal the run can make.
A disagreement between the interpreter and the network should be reported with the case and both readings, because it means either a lowering bug or a part whose contract does not say enough about timing.

Where: `Specs/Parts/Composition.cs`, `Specs/Parts/PartWiring.cs`, `Search/Modules/Promotion.cs`; the n1 x n2 and Fibonacci programs from the previous ticket as tests.

Done when:
- [ ] The hand-written n1 x n2 program lowers to a network that passes the multiply contract on the exhaustive engine and is promoted
- [ ] The hand-written Fibonacci program lowers to a network that gives the 16-value `SequenceTask` exactly, or the report names what timing the lowering could not meet
- [ ] A lowering that needs a missing part names it
- [ ] `dotnet test` green

Read first: RESEARCH.md "Composing modules into machines" (Round timing), "Toward general synthesis" item 3
