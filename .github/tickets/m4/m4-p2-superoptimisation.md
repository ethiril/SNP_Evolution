---
number: 79
title: M4 Part 2: Superoptimisation
milestone: M4
labels:
  - Epic
---

When the algorithm is known, compiling it always gives a correct network, so evolving from scratch is the wrong baseline; what no tool does is take a correct spiking circuit and search for the smallest one that still meets its spec, with a proof.
We want `superopt`: start from a correct network for a spec, search for smaller and faster networks under the hardware profile, and keep only those that pass every step of verification, with synthesis from scratch run on the same spec and budget for comparison.

Its responsibilities are:
* Verification steps for a spec: training cases, held-out cases, a bounded proof, and counterexamples fed back
* The superoptimiser: seeds, edits, a cost map, and re-verification of every elite
* Synthesis from the same spec on the same budget, as the comparison arm

Correctness should be a hard constraint, not a weighted term, because a smaller network that is wrong on one held-out case is not an improvement, and weighting would trade them.
Cost should be `HardwareCost` under the hardware profile, because results that only hold for regex rules do not transfer to integrate-and-fire chips.

Order: after the counterexample loop in M3 Part 3 and the compiled parts in M3 Part 1; verification steps; superoptimiser; synthesis comparison.

Done when: `superopt` on the add and multiply specs, seeded from compiled networks, returns networks smaller than their seeds that pass held-out cases and a bounded proof, and the synthesis arm's results on the same budget are reported next to them.

Read first: RESEARCH.md "Spec to verified circuit", "Proving contracts", "Compile, then shrink" in the README
