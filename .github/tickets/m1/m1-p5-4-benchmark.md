---
number: 30
title: Benchmark composition search against flat runs on Fibonacci
milestone: M1
labels:
  - area: research
  - kind: benchmark
parent: m1-p5-composition-search
---

The method's first claim is that it reaches further than flat search on the same budget.
We want a benchmark of composition search, flat MAP-Elites and the current modular loop on the Fibonacci gaps task, at least 10 seeds each, equal total evaluations (part evolution included), reporting gaps reached, network size and wall time.

Results should be tested with Mann-Whitney U and an effect size, because that is the statistics plan for the paper.
The run commands and seeds should be written down with the results, because they have to be repeatable.

Where: `SNP_Evolution/Evolution/Benchmarking/`; the `benchmark` command in `SNP_Evolution/Cli/CommandLine.cs`; results to RESEARCH.md.

Done when:
- [ ] A results table in RESEARCH.md with medians, the test statistic, p-value and effect size
- [ ] The commands to reproduce it are listed under the table

Read first: Claude Doc "Composing SN P Modules into Machines", section Evaluation and open questions
