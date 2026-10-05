---
number: 23
title: M1 Part 4: Fibonacci by hand from library parts
milestone: M1
labels:
  - Epic
---

We do not yet know whether parts plus typed wiring can even express a growing sequence, so a search over them could fail for reasons nobody could tell apart.
We want the Fibonacci register program built by hand from library parts with typed wiring, as a test and a size target. It is never a seed for evolution.

The program: registers A = 0 and B = 1. Each round fires the output, drains B one unit per step into the output timer, a new A' and a new B', and drains A into B' at the same time (it finishes first since A <= B). After B steps A' = B and B' = A + B; swap and repeat. The gaps are 1, 1, 2, 3, 5, 8 and so on.

The build should use only parts from the library and glue neurons, because the point is to test what composition can express.
Its neuron count should be recorded, because it is the size target composition search and MAP-Elites must beat.

Order: resolve round timing; build and test.

Done when: the hand-built network gives at least 15 Fibonacci gaps exactly on the exhaustive engine, and its size is written to RESEARCH.md.

Read first: Claude Doc "Composing SN P Modules into Machines" (Fibonacci as a register program, Build plan step 4)
