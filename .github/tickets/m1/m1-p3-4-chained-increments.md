---
number: 22
title: Prove typed wiring with two chained increments
milestone: M1
labels:
  - area: modules
parent: m1-p3-wiring-by-port-type
---

Typed wiring is only useful if verified parts, wired correctly, stay correct together.
We want a test that takes two increments from the library (or hand-built ones if the library has none yet), wires the first's done to the second's start and the first's count out to the second's count in, and checks the result against an n + 2 contract on the exhaustive engine with no evolution.

If it fails because of timing (the second part sees its count before or after its start), the test should be kept failing-but-skipped with the reason written down and the finding added to RESEARCH.md, because that answers whether the start/done contract is strict enough and changes Part 1, not this ticket.

Where: `SNP_Evolution.Tests/Evolution/ModuleTests.cs` or a new `CompositionTests.cs`.

Done when:
- [ ] The chained network passes n + 2 for n from 0 to 8, or the test records why not
- [ ] `dotnet test` green

Read first: Claude Doc "Composing SN P Modules into Machines", Build plan step 3 done-when
