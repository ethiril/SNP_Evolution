---
number: 72
title: Look for shared sub-compositions in promoted parts
milestone: M3
labels:
  - area: modules
  - kind: investigation
parent: m3-p4-binary-and-parameterised-parts
---

Promoted parts are kept whole, so a pattern that recurs inside several of them (a register feeding an add, a zero test guarding a decrement) is never a part of its own.
We want to know whether promoted recipes share sub-compositions worth promoting, in the way Stitch and DreamCoder compress solved programs into a library.

The investigation should run over the promoted parts the M3 benchmarks leave behind, because a library of one or two composites has nothing to compress.
It should count, for each candidate sub-composition, how many promoted parts hold it and how much it would shorten their recipes, because a candidate that saves nothing is not worth a contract.
A candidate should only count if it has a contract that can be read off its ports and checked, because a part without a contract cannot be verified or reused.
It should end in a recommendation: build compression into promotion, or leave it, with the numbers behind it.

Where: `SNP_Evolution/Evolution/Modules/Promotion.cs` (recipes), `SNP_Evolution/Evolution/Modules/PartReuse.cs`, the libraries left by the M3 benchmarks; the finding in RESEARCH.md "Toward general synthesis".

Done when:
- [ ] RESEARCH.md lists the candidate sub-compositions found, how many promoted parts hold each, and the recipe size saved
- [ ] It ends with a build-or-leave recommendation and the reason
- [ ] `dotnet test` green

Read first: RESEARCH.md "Modules, compilation and finding what is missing" (Modularity and library learning), "Toward general synthesis" item 7
