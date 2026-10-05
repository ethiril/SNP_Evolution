---
number: 26
title: M1 Part 5: Composition search
milestone: M1
labels:
  - Epic
  - kind: benchmark
---

Flat search scored on output gaps has to find storage, addition, timing and control at once, and a partial version of any one earns no fitness.
We want a search whose genome is a graph of part instances, glue neurons and typed wires, flattened to an ordinary SN P network to be scored, so every engine, task and algorithm still works.

Its responsibilities are:
* The composition genome and its flattening
* Running the existing algorithms over it
* Counting every evaluation, including part runs, side runs and incubation
* The benchmark against flat runs

The run should start from the library alone, with no Fibonacci parts, because the claim is automatic discovery.
Flattening should be deterministic and tag each neuron with its part instance, because Promote in Part 6 needs to recover the structure from a solved network.

Order: genome and flattening; algorithm integration; evaluation accounting; benchmark.

Done when: on the same evaluation budget, composition search reaches more Fibonacci gaps than the current best flat run, over at least 10 seeds, with a Mann-Whitney test and effect size reported in RESEARCH.md.

Read first: Claude Doc "Composing SN P Modules into Machines" (The loop that keeps itself going, Build plan step 5, Evaluation); RESEARCH.md related work on ECGP (Walker and Miller 2008) and CoDeepNEAT
