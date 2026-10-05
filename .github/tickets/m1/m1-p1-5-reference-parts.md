---
number: 11
title: Hand-build a register and a delay as reference parts
milestone: M1
labels:
  - area: modules
parent: m1-p1-contracts-ports-and-readouts
---

The contract task has to be shown to accept a correct part and reject a broken one before evolution is trusted with it.
We want a hand-built delay (done fires k steps after start) and a hand-built register (holds n from a count port until started again, then drains it to a count out-port), each with a contract and a broken copy.

Each broken copy should break exactly one rule (one fires done twice, one leaves a spike behind), because the test is that the right check fails and only that one.
The parts should be built with standard rules (`Rule` with `Consume` set), because those are what evolution produces and what the paper reports.
The register may hold 2n internally if that is what makes it work; record which in a comment, because whether count ports carry n or 2n is an open question in the spec.

Where: a new `SNP_Evolution/Evolution/Contracts/ReferenceParts.cs`, in the style of `SNP_Evolution/Networks/ReferenceNetworks.cs`; tests in `SNP_Evolution.Tests/Evolution/ContractTaskTests.cs`. `SNP_Evolution/Compilation/RegisterMachineCompiler.cs` shows how hand-built ADD and SUB modules are wired and may be reused.

Done when:
- [ ] Both parts score 1.0 on `ContractTask` with the exhaustive engine for n from 0 to 8 and k from 1 to 4
- [ ] Each broken copy fails the intended check and passes the others
- [ ] `dotnet test` green

Read first: Claude Doc "Composing SN P Modules into Machines", Build plan step 1 and the open question on n or 2n
