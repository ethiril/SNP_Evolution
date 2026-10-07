---
number: 82
title: Compare synthesis from a spec with superoptimisation
milestone: M4
labels:
  - area: evolution
  - kind: benchmark
parent: m4-p2-superoptimisation
---

Whether the searches the project already has can find an algorithm from its spec alone, without a compiled seed, is the discovery claim, and it has never been measured against the compile route on one budget.
We want a synthesis arm: the flat search, composition search and part-program search each run on a spec's task with the verifier as admission, and a benchmark that reports them against `superopt` on the same evaluation budget.

Its responsibilities are:
* A `--mode synthesise|superoptimise` option on the spec commands, with synthesis using the existing searches unchanged except for the task and the verifier
* The benchmark: solved counts, evaluations to first verified solution, and the cost of the best verified network, per spec and mode
* Reporting how often synthesis finds a network that passes training but fails held-out cases, the listing failure this milestone exists to stop

Both arms should get the same evaluation budget counted the same way, because otherwise the comparison measures the budget.
Synthesis should not be given the compiled seed in any form, including as a library part, because that would make it superoptimisation.

Where: `Search/Benchmarking/Benchmark.cs`, `Specs/Accounting/Statistics.cs`, `Search/EvolutionSearch.cs` (`CompositionSearch`), the part-program search from M3 Part 2, the superoptimiser from this epic; results in RESEARCH.md.

Done when:
- [ ] Seeds 1 to 10 run in both modes on the add, multiply and Fibonacci recurrence specs, with solved counts, median evaluations, best cost and the training-only rate in RESEARCH.md
- [ ] Mann-Whitney tests with effect sizes compare the modes' costs where both solve
- [ ] `dotnet test` green

Read first: RESEARCH.md "Spec to verified circuit", "Benchmarking and choosing an algorithm" in the README
