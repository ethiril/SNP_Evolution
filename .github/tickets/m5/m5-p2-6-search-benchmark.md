---
number: 126
title: Benchmark every search method on one budget
milestone: M5
labels:
  - area: research
  - kind: benchmark
parent: m5-p2-search-beyond-evolution
---

The claim that exact and rewrite methods beat evolution on small spiking circuits needs one table that runs them all on the same specs, targets and budget.
We want a benchmark over evolution, exact synthesis, windowed improvement, rewrites and enumeration, on timing, binary and count specs, under `generic-if` and one chip target.

The table should give, per spec and method: solved seeds, evaluations and wall time to the first verified network, the best verified size, and whether minimality was proven, because each method wins on a different one of these.
Methods that are deterministic should be run once and marked so, because seeds mean nothing to them.
Wall time should be reported beside evaluations, because one solver call and one simulation are not the same cost.

Where: `SNP_Evolution/Evolution/Benchmarking/Benchmark.cs`, `SNP_Evolution/Evolution/Benchmarking/Statistics.cs`; results in RESEARCH.md.

Done when:
- [ ] The table is in RESEARCH.md for delay 1 to 4, join, bit-serial add, add and multiply
- [ ] Mann-Whitney tests with effect sizes compare evolution with each seeded method where both solve
- [ ] `dotnet test` green

Read first: RESEARCH.md "A verified spiking parts library, and search beyond evolution", "Spec to verified circuit"
