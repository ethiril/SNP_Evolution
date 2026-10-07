---
number: 63
title: Search over part programs
milestone: M3
labels:
  - area: evolution
  - area: cli
parent: m3-p2-programs-of-parts
---

With part programs and a lowering in place, nothing yet finds a program; they are only written by hand.
We want a search over part programs for a contract or sequence task, scored by the interpreter, that lowers, verifies and promotes the first program that solves.

Its responsibilities are:
* Edits: insert, delete or replace a call; rebind a port to another register; redirect a done port; swap a part for another with the same ports
* Scoring with lexicase over the task's checks, as program search and composition search already do
* Lowering and verifying only programs that solve in the interpreter, and promoting the result
* Proposals when the search stalls, reusing `PartProposals` and the target-shape proposer
* A `--genome program|composition` option on `compose` and the benchmark

Evaluations should be counted in the same units as composition search (one interpreter run per case counts as one evaluation of a network), plus every lowered network verified, because the two genomes must be compared on one budget.
A program that solves in the interpreter but fails after lowering should be kept out of the population and logged, because the interpreter would otherwise reward it forever.

Where: new search beside the part-program interpreter; `Search/EvolutionSearch.cs` (`CompositionSearch`) for the edit and evaluation conventions, `Search/Proposals/PartProposals.cs`, `Cli/Commands/ComposeCommand.cs`, `Search/Benchmarking/Benchmark.cs`.

Done when:
- [ ] `compose --task "Contract multiply" --genome program` solves from a library with no hand-built parts on at least half of 10 seeds, with the median evaluations reported next to composition search
- [ ] A program that fails after lowering is logged and dropped
- [ ] `dotnet test` green

Read first: RESEARCH.md "Toward general synthesis" item 3, README "Composing machines from parts"
