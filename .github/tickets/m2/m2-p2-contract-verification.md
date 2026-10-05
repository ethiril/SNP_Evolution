---
number: 42
title: M2 Part 2: Verify contracts formally
milestone: M2
labels:
  - Epic
---

A contract passed on the test cases says nothing about the inputs just past them, and composition trusts every part completely.
We want a bounded proof that a part meets its contract for every input up to a bound, and an export to a model checker for proofs past it.

Its responsibilities are:
* A bounded check over every input up to N on the exhaustive engine, with a counterexample on failure
* An export to Uppaal timed automata, following the published translation of SN P systems

The bounded check should run before a part enters the library, because a part admitted on tests alone can fail inside a composition at a value nobody tested.

Order: bounded check; Uppaal export.

Done when: every part in the library has a recorded bound to which it is proven, and one part is verified in Uppaal for a stated property.

Read first: RESEARCH.md "Use cases and a practical path" (item 6 and its sources)
