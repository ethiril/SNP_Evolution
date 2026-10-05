---
number: 32
title: Promote solved compositions to parts
milestone: M1
labels:
  - area: modules
parent: m1-p6-promotion-proposals-arithmetic
---

`ModuleLibrary.MaxModuleNeurons` caps every module at 24 neurons, so a solved composition can never become a part and there is no hierarchy.
We want a solved composition saved as a part whose contract is the target's, stored as a reference to its child parts plus glue and wires rather than a copy of the flattened network.

The cap should apply to evolved leaf parts only, because a composite's size is its children's and they were each verified.
A promoted part should be re-verified on its own contract before it is stored, because the composition was scored on the target task, which may test less than the contract.
Promoting should be automatic when a composition run solves its task, and logged.

Where: `SNP_Evolution/Evolution/Modules/ModuleLibrary.cs`, the `Composition` type from Part 5, the library folder format from Part 2.

Done when:
- [ ] The two-increment chain promoted as "add 2" loads from disk and flattens to the same network
- [ ] A composite larger than 24 neurons is accepted
- [ ] `dotnet test` green

Read first: Claude Doc "Composing SN P Modules into Machines", section The loop that keeps itself going (Promote)
