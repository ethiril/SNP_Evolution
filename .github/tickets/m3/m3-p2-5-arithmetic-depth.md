---
number: 65
title: Build arithmetic three levels deep
milestone: M3
labels:
  - kind: benchmark
  - area: research
parent: m3-p2-programs-of-parts
---

Promotion has been shown one level deep (the add loop under multiplication), which does not show that the library compounds.
We want subtraction, division and comparison in count encoding, and then n^k, each found by search from the library as it grows, with each solution promoted before the next target runs.

The targets should run in one library, in order, because reuse of earlier promotions is what is being measured.
Reuse should be reported per level: which promoted parts appear in each solution, nested copies included, because Berlot-Attwell et al. (2024) found learned libraries rarely reused.
An n^k contract should be added to `ArithmeticParts` with a specification, keeping its cases small enough for the exhaustive engine, because it has to be verifiable to count.
Sizes and steps should be added to the published-circuit table, because Zeng et al. give hand-built sizes for the same four operations.

Where: `SNP_Evolution/Evolution/Contracts/ArithmeticParts.cs`, `SNP_Evolution/Evolution/Contracts/Specification.cs`, `SNP_Evolution/Evolution/Modules/PartReuse.cs`; results in RESEARCH.md "Evolved against hand-designed arithmetic".

Done when:
- [ ] Solved counts over 5 seeds for subtraction, division, comparison and n^k, by part-program and by composition search, are in RESEARCH.md
- [ ] At least one n^k solution holds a promoted multiplier that holds a promoted add loop, and the reuse table shows it
- [ ] `dotnet test` green

Read first: RESEARCH.md "Evolved against hand-designed arithmetic", "Toward general synthesis" item 8
