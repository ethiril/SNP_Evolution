---
number: 75
title: Add algorithm specs and their cases
milestone: M4
labels:
  - area: tasks
  - area: modules
parent: m4-p1-specs-as-targets
---

A contract is a list of cases and a `Specification` is found only by a catalogue contract's name, so there is no way to hand the tool a new algorithm and have it make the cases.
We want an `AlgorithmSpec`: a name, a reference function from named inputs to named outputs, an input domain with a size parameter n, a port encoding per input and output, and a latency bound, from which a contract and its cases are made.

Its responsibilities are:
* The spec type, holding a reference function and, for later tickets, an optional checker
* Case generation: training cases up to a size n, held-out cases above it, and a slot for counterexamples, kept apart
* Turning a spec into a `Contract` and its `Specification`, so `ContractTask`, `BoundedCheck` and the part searches take it unchanged
* Specs for the arithmetic contracts already in `FirstParts` and `ArithmeticParts`

The spec should extend the `Specification` that M2.5 made the source of each contract, not sit beside it, because two definitions of one function is the duplication M2.5 removed.
Held-out cases should be drawn from larger inputs than any training case, because generality is about inputs the search never saw, and a random split inside one range does not test that.
Case generation should be seeded and deterministic, because two runs of one spec must score the same networks the same way.
A spec built for a catalogue contract should agree with that contract's cases and its `Specification`, checked with `Specifications.Disagreements`, because the catalogue is what existing parts were verified against.
Training cases should include the edges of the domain (zero, one, the largest training value), because off-by-one parts pass the middle and fail the edges.

Where: new `Specs/Contracts/AlgorithmSpec.cs`; `Specs/Contracts/Specification.cs`, `Specs/Contracts/Contract.cs`, `Specs/Contracts/PortEncoding.cs`, `Specs/Contracts/ArithmeticParts.cs`, `Specs/Tasks/ContractTask.cs`; tests in `SNP_Evolution.Tests/Specs/`.

Done when:
- [ ] Specs for add, multiply, compare and fan-out make contracts with no disagreements against the catalogue's
- [ ] A spec gives the same training and held-out cases on two runs with one seed, and no held-out input is inside the training range
- [ ] The hand-built add part scores 1 on both the training and the held-out cases of the add spec
- [ ] `dotnet test` green

Read first: RESEARCH.md "Spec to verified circuit", README "Proving parts past their cases"
