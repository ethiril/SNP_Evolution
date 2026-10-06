---
number: 64
title: Find Fibonacci from parts with nothing hand-wired
milestone: M3
labels:
  - kind: benchmark
  - area: research
parent: m3-p2-programs-of-parts
---

Fibonacci was the test this whole direction was chosen for, and so far it has only been built from parts by hand (27 neurons) or compiled from a fitted recurrence (15, shrunk to 10).
We want the 16-value Fibonacci `SequenceTask` found by part-program search from a library with no hand-built parts, with the target-shape proposer on.

The round timing should be left to the search and the lowering, not given as offsets, because the stored-low banks (A - 2, B - 3) are a hand derivation.
The report should compare size against the hand-built composition and the compiled and shrunk networks, because those are the two existing answers.
If the search does not solve it, the report should say where it stops (best fitness, the values reached, what the lowering could not time), because that is the finding.
The same run should be repeated on one other recurrence (such as gaps 2^k or g(n) = g(n-1) + 2g(n-2)), because a method that only finds Fibonacci is a Fibonacci method.

Where: the part-program search and `compose`; results in RESEARCH.md "Composing modules into machines" and the README.

Done when:
- [ ] Seeds 1 to 10 run on Fibonacci and on one other recurrence, with solved counts, median evaluations and sizes in RESEARCH.md
- [ ] Any solution is verified on the exhaustive engine and checked on the next 4 values past the 16 given
- [ ] `dotnet test` green

Read first: RESEARCH.md "Composing modules into machines", "Toward general synthesis" items 3 and 8
