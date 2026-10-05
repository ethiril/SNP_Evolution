---
number: 28
title: Run the existing algorithms over compositions
milestone: M1
labels:
  - area: evolution
parent: m1-p5-composition-search
---

A new genome is only worth having if the existing GA, MAP-Elites and the iterative loop can search it.
We want a composition search mode in which individuals are flattened compositions, mutation is drawn from the Part 3 wiring mutations plus insert-part and remove-part, and plain neuron mutations act only on glue neurons.

Its responsibilities are:
* A mutation mix for composition mode, configurable in settings
* Freezing part neurons (as `ProtectModules` does today) so only glue and wiring change
* A catalogue entry so the mode can be picked from the command line and the menu

Crossover should swap whole part instances with their wires, or be disabled in this mode, because a cut through the middle of a part voids its verification.
The mode should be selectable for any task, not just Fibonacci, because the paper claims a general method.

Where: `SNP_Evolution/Evolution/Modules/ModularEvolution.cs`, `SNP_Evolution/Evolution/IterativeEvolution.cs`, `SNP_Evolution/Cli/Catalog.cs` (`Algorithms`) and `AlgorithmCatalog`, `SNP_Evolution/Cli/Settings.cs`.

Done when:
- [ ] `dotnet run -- benchmark --algorithm <composition mode> --task <a function task> --seeds 1` runs end to end (the mode is listed by `dotnet run -- algorithms`)
- [ ] A test shows part neurons are never changed by a run in this mode
- [ ] `dotnet test` green

Read first: Claude Doc "Composing SN P Modules into Machines", Build plan step 5
