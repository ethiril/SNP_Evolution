---
number: 68
title: Prove compositions from their parts' proofs
milestone: M3
labels:
  - area: modules
  - area: simulation
parent: m3-p3-proofs-that-scale
---

A promoted part is proven by flattening it onto the exhaustive engine, so each level of the hierarchy costs more to prove than the last, and deep machines will never be proven past their cases.
We want a proof by composition: a composition meets its contract up to N when every child is proven up to the largest value it can receive at N, and the composition uses every child as its contract assumes.

Its responsibilities are:
* A use check: each child is started only after its previous done (or never twice), receives inputs only through its in-ports and only between its start and done as its contract allows, and is read only through its out-ports
* A value bound: the largest value each child can receive when every task input is at most N, from the part program or from the children's specifications along the wires
* The proof itself: specification-level evaluation of the composition (the part program, or the wiring) for every input up to N, with each child replaced by its specification
* The result recorded in the part file as proven by composition, with the bound and the children's bounds it relied on

The use check should be stated as what the contract's cases assume and nothing more, because a proof by composition is unsound wherever a part is used outside what its contract tested.
The timing of a child should come from its specification's latency bound, and a composition that relies on exact timing between children should be refused with the reason, because start and done are what make composition timing-safe, and anything else is unproven.
Where the flattened check and the proof by composition both run, they should agree, and a test should cover this, because the new proof is only trusted where it has been checked.

Where: `Specs/Verification/BoundedCheck.cs`, `Specs/Contracts/Specification.cs`, `Search/Modules/Promotion.cs` (recipes), the `verify` command; tests in `SNP_Evolution.Tests/Specs/Verification/BoundedCheckTests.cs`.

Done when:
- [ ] The add loop and the promoted multiplier are proven by composition to at least five times their flattened bounds in the same time
- [ ] A composition that starts a child twice without waiting for its done, or feeds it mid-run, is refused with the child and the step
- [ ] Both proofs agree on every promoted part up to the flattened bound
- [ ] `dotnet test` green

Read first: RESEARCH.md "Proving contracts", "Toward general synthesis" item 5
