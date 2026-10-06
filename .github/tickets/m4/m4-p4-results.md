---
number: 86
title: M4 Part 4: Results
milestone: M4
labels:
  - Epic
---

The claim this milestone exists to support is that a spec goes in and the smallest verified circuit comes out, and that needs numbers against circuits people designed by hand, plus evidence that the circuits are general and not lists.
We want three results: published arithmetic circuits matched or beaten in size with proofs, recurrence generators shown general on held-out starting values, and an investigation into proofs for every n.

Its responsibilities are:
* The flagship benchmark: arithmetic against the published hand designs, in count and binary encodings
* The generality benchmark: the recurrence family, including Fibonacci
* An investigation of inductive proofs that hold for every n

Order: arithmetic records (after the binary adder in M3 Part 4 for its binary rows); recurrences; inductive proofs.

Done when: RESEARCH.md has the arithmetic table with our verified sizes beside the published ones, the recurrence results with held-out pass rates, and a written answer on whether all-n proofs are feasible for loops of parts.

Read first: RESEARCH.md "Spec to verified circuit", "Evolved against hand-designed arithmetic"
