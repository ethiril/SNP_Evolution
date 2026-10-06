---
number: 66
title: M3 Part 3: Proofs that scale
milestone: M3
labels:
  - Epic
---

Bounded proofs run on the flattened network, so the cost grows with the whole hierarchy (the add loop reached N = 30 in two minutes), and a counterexample ends a part's admission without teaching the search anything.
We want counterexamples fed back into the search as cases, and compositions proven from their children's proofs rather than flattened.

Its responsibilities are:
* Counterexample-guided search: a counterexample becomes a case the next search must pass
* Proof by composition: a composition's bound from its children's bounds and a check that each child is used as its contract assumes

The flattened bounded check should stay as the reference, because a proof by composition is only trusted once it agrees with it where both run.

Order: counterexample-guided search; proof by composition.

Done when: a part that passes its cases but fails past them is repaired by the search using its counterexample, and the add loop is proven by composition to a bound at least five times the flattened one in the same time.

Read first: RESEARCH.md "Proving contracts", "Toward general synthesis" (items 4 and 5)
