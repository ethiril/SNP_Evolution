---
number: 74
title: M4 Part 1: Specs as targets
milestone: M4
labels:
  - Epic
---

Every task so far scores fixed values (a target sequence or a contract's listed cases), so a network that lists the answers scores the same as one that computes them, and nothing asks for a solution that holds for every n.
We want an algorithm spec as a first-class target: a reference function or checker over an input domain, with an encoding onto ports, from which training, held-out and counterexample cases are drawn.

Its responsibilities are:
* The spec type and its case generation, with the function kind turned into a contract task
* A generator kind for recurrences, scored on the rule from varied starting values rather than on one sequence
* Relation specs, where any output a checker accepts is right
* A stream kind for sensor-in, actuator-out behaviour, on top of the streaming task

`Specification` should stay the source of truth for contracts that already have one, because bounded proofs already trust it and a second definition of add would drift.
Fibonacci should be a test of the generator kind and not a special case anywhere, because the target is any algorithm the user can write as a spec.

Order: spec type and function kind; generator kind; relation specs; stream kind.

Done when: add, multiply, compare and the Fibonacci recurrence are each written as one spec, and each produces a task whose held-out cases a listing network fails.

Read first: RESEARCH.md "Spec to verified circuit", "Toward general synthesis"
