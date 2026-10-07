---
number: 56
title: Add control parts: join, fork, merge and select
milestone: M3
labels:
  - area: modules
parent: m3-p1-parts-without-hand-building
---

The hand-built add loop was wrong past its cases until its done waited for two dones, and that join was a glue rule written by hand; composition has no vocabulary for control.
We want four control contracts in the first-part catalogue, evolved, verified and kept in the library like the delays.

Its responsibilities are:
* Join: two trigger in-ports; done fires once, after both have fired, in either order and with any gap up to a stated bound
* Fork: one start; two trigger outs firing on the same step
* Merge: two trigger in-ports, never both in one round; done fires once after whichever fired
* Select: start and a trigger data in-port; done-one fires when the data port fired, done-zero when it did not

Join's cases should include both orders, equal steps, and the largest gap the contract allows, because a join that only works when its inputs arrive together is a relay.
Each contract should leave every neuron as it began, as every first part does, because a join left holding one spike fires early next round.
The contracts should be general, not taken from the add loop, because they are goals for automatic discovery.

Where: `Specs/Contracts/FirstParts.cs` (catalogue and `Table()`), `Specs/Contracts/Specification.cs` (a specification for each), `Search/PartSearch.cs`; tests in `SNP_Evolution.Tests/Specs/Contracts/FirstPartsTests.cs`.

Done when:
- [ ] The four contracts are in the catalogue with specifications that agree with their cases
- [ ] `evolve-parts --only "join,fork,merge,select"` over seeds 1 to 5 reports how many seeds solve each, unrestricted and with `--profile hardware`, and the results are in RESEARCH.md
- [ ] Every part solved is proven by `verify` and saved to `parts/` (and `parts-profile/` for profile runs)
- [ ] `dotnet test` green

Read first: RESEARCH.md "Toward general synthesis" item 1, README "Evolving library parts"
