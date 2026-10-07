---
number: 130
title: Specify state machines by transition table
milestone: M5
labels:
  - area: tasks
parent: m5-p3-verified-library
---

Controllers inside spiking agents and robots are small state machines, today built as approximate attractor networks with no proof, and a stream spec written as code is more than most users want to write.
We want a state machine spec: states, input trigger ports, output trigger ports, and a transition table, turned into a stream spec whose reference is the table.

Its responsibilities are:
* The table form, readable from a spec file
* The translation to a stream spec, with input trains that cover every transition
* Examples: the debouncer, a two-phase handshake, and a sequencer that can be reset

Generated trains should cover every transition at least once in training and in held-out runs, because a transition never exercised is never checked.
Simultaneous inputs should be stated in the table (a priority, or an error output), because otherwise the machine's behaviour on them is undefined.

Where: the stream spec from M4 Part 1 and the spec file format from M4 Part 3; `Specs/Tasks/StreamingTask.cs`.

Done when:
- [ ] The three examples are tables that make stream specs, and the debouncer's matches the existing debouncer task
- [ ] Each has a verified network under `generic-if`
- [ ] `dotnet test` green

Read first: RESEARCH.md "A verified spiking parts library, and search beyond evolution" (Where it matters in practice, item 2)
