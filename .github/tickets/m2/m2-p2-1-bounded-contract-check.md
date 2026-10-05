---
number: 43
title: Prove contracts for every input up to a bound
milestone: M2
labels:
  - area: simulation
  - area: modules
parent: m2-p2-contract-verification
---

Test cases cover n from 0 to 8 and one larger value; nothing checks the rest.
We want a `verify` step that runs a part's contract on every input combination up to a bound N on the exhaustive engine, reports the first counterexample (input, rule broken, step trace), and records the largest N proven in the part's library file.

The check should increase N until a time limit and keep the largest proven bound, because a fixed N is either too small to mean anything or too slow for big parts.
An inexact exhaustive result should count as not proven, because sampling is not proof.

Where: `SNP_Evolution/Simulation/ExhaustiveCpuEngine.cs`, `SNP_Evolution/Evolution/Tasks/ContractTask.cs`, the library folder format; a `verify` command in `SNP_Evolution/Cli/CommandLine.cs`.

Done when:
- [ ] The register reference part is proven to some N of at least 32 within a minute
- [ ] A part that fails only at n = 20 is caught, with its counterexample printed
- [ ] `dotnet test` green

Read first: RESEARCH.md "Use cases and a practical path", item 6
