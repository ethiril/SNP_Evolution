---
number: 89
title: Find out whether loops of parts can be proven for every n by induction
milestone: M4
labels:
  - area: research
  - kind: investigation
parent: m4-p4-results
---

Bounded proofs stop at a bound, and proving an SN P system correct for every input is undecidable in general, but a loop of verified parts has the structure an induction needs.
We want a written answer, with a prototype if it is feasible, on proving a loop of parts correct for every n: that one round maps registers holding the loop's values to registers holding the next ones and leaves every other neuron as it was, and that each part's contract holds for every input.

Its responsibilities are:
* Encoding one round of a part's spike counts in Presburger arithmetic, using the eventually periodic form the rule conditions already compile to
* k-induction over the encoded step, as Kind 2 and IC3-style model checkers do, as the route to properties that hold for all time, beside induction over loop rounds
* A prototype with Z3 on the add part and the Fibonacci round, if the encoding works
* A written result: what was proven, what could not be encoded, and how long the solver took

The investigation should say which networks the method cannot handle (flat evolved networks, rules outside the fragment, delays), because an inductive proof that only some networks admit is still useful if the limit is stated.
The prototype should be checked against `BoundedCheck` where both run, because a proof method is only trusted once it agrees with the bounded one.
The step encoding should be written so the SMT encoding in M5 can reuse it, because exact synthesis and induction need the same terms for one step.
The external solver should be optional and its tests should skip when it is missing, as the Uppaal and iverilog tests do, because not every machine has it.

Where: `SNP_Evolution/Evolution/Contracts/BoundedCheck.cs`, `SNP_Evolution/Simulation/` (the lasso tables for rule conditions), `SNP_Evolution/Export/ExternalTool.cs`; Pérez-Jiménez et al. 2024 in RESEARCH.md "Proving contracts" for the by-hand method; results in RESEARCH.md.

Done when:
- [ ] RESEARCH.md says whether one round of the add part and of the Fibonacci round can be encoded and proven for every input, with solver times, or why not
- [ ] If a prototype exists, it agrees with `BoundedCheck` up to the bounded check's bound, and its tests skip without Z3
- [ ] `dotnet test` green

Read first: RESEARCH.md "Proving contracts", "Spec to verified circuit", "A verified spiking parts library, and search beyond evolution" (Proofs for every input)
