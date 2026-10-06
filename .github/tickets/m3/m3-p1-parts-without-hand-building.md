---
number: 55
title: M3 Part 1: Parts without hand-building
milestone: M3
labels:
  - Epic
---

Every machine with count ports so far is built on hand-built parts, because `evolve-parts` has never solved a count-port contract, so the automatic-design claim stops at delays and sequencers.
We want the count first parts and a set of control parts found with no hand-built input, and n1 x n2 composed from them.

Its responsibilities are:
* Control parts: join, fork, merge and select, evolved like the first parts
* Register programs that compute a function of their inputs, searched against a contract's cases
* Compiling such a program into a part with start, done and count ports, then verifying and shrinking it
* A benchmark of the compiled route against the hand-built parts

The hand-built parts should stay opt-in and unchanged, because they are the control the new route is measured against.
The paper should be able to say which modules the compiler is built from, because textbook ADD and SUB modules are given knowledge, though not given parts.

Order: control parts; function programs; compiling programs to parts; benchmark.

Done when: `parts/` holds a verified register, add, zero test, fan-out and increment found with no hand-built input, and n1 x n2 in count encoding is solved by composition search with `--hand-built off`.

Read first: RESEARCH.md "Toward general synthesis" (items 1 and 2), "What was built for 7 and 9 (`compile`)"
