---
number: 119
title: M5 Part 2: Search beyond evolution
milestone: M5
labels:
  - Epic
---

Every search so far is evolutionary, which cannot say a circuit is the smallest, and spends most evaluations on candidates that behave like ones already seen; exact and rewrite-based methods do both better on small circuits, and none has been applied to spiking circuits.
We want three more search methods behind the common search interface, each usable by `superopt` and the synthesis arm: SMT exact synthesis with counterexamples, windowed local improvement, and rewrites proven once, plus an enumerative baseline, compared with evolution on one budget.

Its responsibilities are:
* An SMT encoding of a network's run over a bounded number of steps, checked against the engine
* Exact synthesis of a small part from its spec, with a minimality result
* Windowed local improvement: cut a part or a few neighbours out of a correct network and resynthesise it smaller
* Rewrite rules proven once on the exhaustive engine, applied by search
* An enumerative baseline that prunes candidates behaving like ones already seen
* A benchmark of all methods against evolution on the same specs and budget

Each method should be a search behind the interface from M2.5, counted in the same evaluation units, because the comparison is only fair if every method spends the same budget the same way.
Every result should pass the same verifier as evolution's, because a method that skips verification is not comparable.
The SMT solver should be optional and its tests should skip without it, as iverilog's do, because not every machine has it.

Order: SMT encoding; exact synthesis; windowed improvement; rewrites; enumeration; benchmark.

Done when: on the delay, join, bit-serial adder and add specs, each method's best verified network and evaluations are reported beside evolution's, and exact synthesis proves at least one library part minimal for its bound.

Read first: RESEARCH.md "A verified spiking parts library, and search beyond evolution", "Spec to verified circuit"
