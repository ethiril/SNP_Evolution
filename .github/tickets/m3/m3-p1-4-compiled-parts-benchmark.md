---
number: 59
title: Benchmark compiled parts against hand-built ones
milestone: M3
labels:
  - kind: benchmark
  - area: research
parent: m3-p1-parts-without-hand-building
---

The compiled route is only a result if the parts it finds are measured against the hand-built ones and composition works without the hand-built seed.
We want `evolve-parts --route compile` run over the count first parts and the building blocks, and n1 x n2 composed from what it finds.

The table should give, per contract, seeds solved, evaluations, and neurons, synapses and rules after shrinking, next to the hand-built part, because size is the comparison readers will make.
The n1 x n2 runs should repeat the M1 benchmark settings (10 seeds per algorithm, 6000 evaluations, lexicase, 5 sampled runs) with `--hand-built off`, because only then is the change attributable to the library.
If n1 x n2 is not solved, the report should say so and give the best fitness and the parts the best networks hold, because an honest negative is still the result.

Where: `SNP_Evolution/Evolution/Benchmarking/Benchmark.cs`, the `evolve-parts` and `benchmark` commands; results in RESEARCH.md "Evolved against hand-designed arithmetic" and README "Composing machines from parts".

Done when:
- [ ] The per-contract table is in RESEARCH.md, seeds 1 to 5
- [ ] n1 x n2 results with `--hand-built off` are in RESEARCH.md and the README, with Fisher's exact test against the `--hand-built leaves` control
- [ ] `parts/` is updated with the kept compiled parts
- [ ] `dotnet test` green

Read first: RESEARCH.md "Evolved against hand-designed arithmetic", "Toward general synthesis" item 2
