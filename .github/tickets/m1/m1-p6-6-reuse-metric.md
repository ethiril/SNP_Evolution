---
number: 37
title: Measure how often promoted parts are reused
milestone: M1
labels:
  - area: evolution
  - area: research
parent: m1-p6-promotion-proposals-arithmetic
---

A library that grows but is never used adds nothing, and earlier work on learned libraries found exactly that.
We want each run to report, per library part, how many instances appear in the best solution and in the final population, and a summary of reuse across a benchmark's seeds.

Counting should use `ModuleTag` on flattened networks, so it works for any algorithm.
The existing `Module.Uses` and `Wins` counters should either be reused or explained in a comment, because two reuse numbers that disagree would confuse the paper.

Where: `SNP_Evolution/Evolution/Modules/ModuleLibrary.cs`, `SNP_Evolution/Evolution/Modules/ModuleTracker.cs`, run summaries in `SNP_Evolution/Cli/EvolutionSession.cs`.

Done when:
- [ ] A run summary lists reuse per part
- [ ] The benchmark output includes reuse
- [ ] `dotnet test` green

Read first: Claude Doc "Composing SN P Modules into Machines", section Evaluation and open questions (Reuse); Berlot-Attwell et al. (2024), https://arxiv.org/abs/2410.20274
