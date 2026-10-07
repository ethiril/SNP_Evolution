---
number: 87
title: Benchmark superoptimised arithmetic against published circuits
milestone: M4
labels:
  - area: research
  - kind: benchmark
parent: m4-p4-results
---

The published SN P and neuromorphic arithmetic circuits were all designed by hand, and our table has their sizes beside ours but no evolved circuit that is smaller and proven.
We want the adder, subtracter, multiplier and divider, in count and binary encodings, superoptimised from their specs, verified, and reported against the published sizes.

Its responsibilities are:
* Specs for the four operations in count encoding and in binary at widths 1 to 4
* `superopt` runs per spec, with and without the hardware profile
* The table: neurons, synapses, rules, distinct rules and steps, our verified best against Zeng et al. 2012, Liu et al. 2015, Chen and Guo 2023, von Seeler et al. 2025 and Aimone et al.'s streaming adder

Each row should say what was proven (the bound, or held-out only), because a smaller circuit that is less proven is not like for like.
Rows should be compared only within one encoding, because a unary circuit costs time in the value and a binary one in the width.
The report should say that our seeds come from our own compiler and not from the published designs, because the published circuits could not be opened and rebuilt.
Binary rows should wait for the binary adder from M3 Part 4, because no binary contract has been solved yet.

Where: the specs from M4 Part 1, `superopt` from M4 Part 2, `Specs/Contracts/ArithmeticParts.cs`, `Specs/Parts/HardwareCost.cs`; the table in RESEARCH.md "Evolved against hand-designed arithmetic".

Done when:
- [ ] Seeds 1 to 10 run per spec, with and without the profile, and the best verified network per spec is saved under `parts/` or `parts-profile/`
- [ ] The table in RESEARCH.md gives our verified sizes and bounds beside every published row they can be compared with
- [ ] For each published row, the report says whether we matched it, beat it, or did not, and by how much
- [ ] `dotnet test` green

Read first: RESEARCH.md "Evolved against hand-designed arithmetic", "Spec to verified circuit"
