---
number: 60
title: M3 Part 2: Programs of parts
milestone: M3
labels:
  - Epic
---

Composition search changes one wire at a time and is scored by simulating the flattened network, so it finds a loop only when a loop part is already in the library, and Fibonacci from parts has only ever been wired by hand.
We want a genome one level up: a short program whose instructions call library parts on named registers, scored by an interpreter over the parts' specifications, and lowered to a composition only when it solves.

Its responsibilities are:
* A part-program form and an interpreter that runs each call through the part's `Specification`
* Lowering a part program to a `Composition` that flattens, verifies and promotes like any other
* A search over part programs, with proposals when it stalls
* Fibonacci from parts with nothing hand-wired
* Arithmetic three levels deep, with reuse reported at each level

The interpreter should never simulate a network, because the point is to score programs at the cost of `ProgramSearch`, not of composition search.
Lowering should be checked against the interpreter on every case, because the interpreter's answer is only worth anything if the network agrees.
Flat composition search should stay, for shrinking and tuning glue, because the program level gives structure, not size.

Order: part programs and interpreter; lowering; search; Fibonacci from parts; arithmetic depth.

Done when: Fibonacci (16 values) and n^k are each found by part-program search from a library with no hand-built parts, verified on the exhaustive engine, and promoted.

Read first: RESEARCH.md "Toward general synthesis" (items 3 and 8), "Composing modules into machines" (Round timing, Fibonacci by hand from library parts)
