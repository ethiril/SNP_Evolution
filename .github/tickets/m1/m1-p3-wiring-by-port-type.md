---
number: 18
title: M1 Part 3: Wiring by port type
milestone: M1
labels:
  - Epic
---

`ModuleEdits.Insert` wires each module port to a random neuron, so chaining parts on purpose cannot be expressed and almost every insertion breaks the host.
We want parts inserted and connected by port type, a done to a start and a count out to a count in, plus mutations that change those wires.

Its responsibilities are:
* Parts in the library expose their typed ports as neuron positions
* Typed insertion and wiring
* Mutations: rewire a port, add a glue neuron on a wire, swap a part for a cheaper one with the same contract

Wires should only join compatible ports (same kind, same binary width, out to in, done to start), because a mistyped wire is never right and spending evaluations on it is waste.
The harvested, uncontracted modules should keep their current random insertion, because the existing modular loop must not regress.

Order: typed ports on parts; typed insertion; the mutations; the chained-increments test.

Done when: two verified increments wired done to start and count out to count in pass the n + 2 contract with no evolution at all, and `dotnet test` stays green.

Read first: Claude Doc "Composing SN P Modules into Machines" (Build plan step 3)
