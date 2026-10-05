---
number: 17
title: Add an evolve-parts command
milestone: M1
labels:
  - area: cli
parent: m1-p2-evolve-first-parts
---

Nothing yet runs the contracts and fills the library.
We want `evolve-parts` on the command line (and an entry in the main menu) that, for each first-part contract not already solved in the library, evolves a part with `ContractTask`, verifies it exhaustively, shrinks it with MAP-Elites on hardware cost, and saves it.

Its responsibilities are:
* Options: `--seed`, `--budget` per contract, `--only` to run a subset, `--library` folder, `--engine`
* A summary table at the end: contract, solved or not, evaluations used, neurons, synapses, latency
* Skipping contracts already solved, unless `--redo on` (options are `--name value` pairs, as `CommandLine.Run` parses them)

The same seed should give the same library, because the paper reports these runs and a reviewer must be able to repeat them.
Evaluations should be counted and reported per contract, because the budget comparison in Part 5 counts part runs against flat runs.

Where: `SNP_Evolution/Cli/CommandLine.cs` (the `switch` over `args[0]`, and `Usage`), `SNP_Evolution/Cli/MainMenu.cs`, `SNP_Evolution/Cli/Catalog.cs` for engine choice; the algorithm from `Catalog.StructuralDefault`.

Done when:
- [ ] `dotnet run -- evolve-parts --seed 1` solves at least delay, fan-out, add and the timer from scratch on the default budget
- [ ] Running twice with the same seed writes identical files
- [ ] The summary table prints; `README.md` documents the command
- [ ] `dotnet test` green

Read first: Claude Doc "Composing SN P Modules into Machines", Build plan step 2
