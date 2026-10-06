---
number: 77
title: Add relation specs with a checker
milestone: M4
labels:
  - area: tasks
  - area: modules
parent: m4-p1-specs-as-targets
---

Solving an equation usually means finding any x with R(input, x), such as a divisor of n or x with a x = b, and a reference function can only say which one answer is right.
We want relation specs: instead of a reference function, a checker that takes the inputs and what the network read out and says whether it is acceptable, scored on every computation of a nondeterministic network.

Its responsibilities are:
* A checker in the spec, used in place of the reference function when one is given
* Scoring and `BoundedCheck` that accept any output the checker accepts, on every computation
* A "no answer" done port for inputs where no output exists, checked by the spec's domain

Every computation should have to satisfy the checker, not just one, because a part must be correct on whichever choice the hardware makes.
A spec with no solution for an input should say so in its domain, and the part should answer on its no-answer done port, because a part that never fires done cannot be composed.
The checker should not be used to make expected values for the case text, because two parts that give different valid answers read differently but are both right.

Where: the spec type from this epic; `SNP_Evolution/Evolution/Tasks/ContractTask.cs`, `SNP_Evolution/Evolution/Contracts/BoundedCheck.cs`, `SNP_Evolution/Evolution/Contracts/Specification.cs`, `SNP_Evolution/Simulation/ExhaustiveCpuEngine.cs`.

Done when:
- [ ] A "some divisor of n other than 1 and n, or no answer" spec scores a hand-built nondeterministic network as solved when every computation gives a valid divisor
- [ ] `BoundedCheck` on a relation spec reports a counterexample with the read output when one computation gives an invalid answer
- [ ] `dotnet test` green

Read first: RESEARCH.md "Spec to verified circuit", "Proving contracts"
