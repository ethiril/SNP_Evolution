---
number: 15
title: Deduplicate library parts by behaviour
milestone: M1
labels:
  - area: modules
parent: m1-p2-evolve-first-parts
---

`ModuleLibrary.Add` keys modules by `Cut.Key`, the network's notation, so two networks that compute the same thing are kept twice and a smaller equivalent never replaces a larger one.
We want a contract part's identity to be its contract plus its results on every contract case, and adding an equivalent part to keep only the cheaper by `HardwareCost`.

The harvested, uncontracted modules of the existing modular loop should keep working as they do now, because that loop is the current paper's result and must not regress.
Replacing a part should keep its id, so anything that refers to it by id still resolves, and log the swap through the library's existing `log` callback.

Where: `SNP_Evolution/Evolution/Modules/ModuleLibrary.cs` (`Module`, `Add`); `SNP_Evolution/Evolution/Modules/ModuleCuts.cs` (`Cut.Key`). Tests in `SNP_Evolution.Tests/Evolution/ModuleTests.cs`.

Done when:
- [ ] Adding a larger part with the same behaviour is a no-op; adding a smaller one replaces it under the same id
- [ ] Two parts with different results on one case are both kept
- [ ] Existing `ModuleTests` pass unchanged
- [ ] `dotnet test` green

Read first: Claude Doc "Composing SN P Modules into Machines", sections Where the current module system falls short and The loop that keeps itself going (Store)
