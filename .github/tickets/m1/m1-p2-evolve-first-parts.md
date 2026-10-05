---
number: 12
title: M1 Part 2: Evolve the first parts and keep a library on disk
milestone: M1
labels:
  - Epic
---

There are no verified parts to compose. The library today holds cuts harvested during a run, deduplicated by structure and gone when the run ends.
We want a command that evolves a fixed set of general arithmetic parts from scratch, verifies each against its contract, shrinks it, and saves the smallest per contract to a library folder that later runs load.

Its responsibilities are:
* The ten first contracts, none specific to Fibonacci
* Hardware cost measures, used to pick the smaller of two equal parts
* Deduplication by behaviour rather than structure
* A library on disk: parts, their contracts and their port bindings
* An `evolve-parts` command, repeatable with `--seed`

The goals should stay general arithmetic, because the default of automatic discovery is the paper's claim and a Fibonacci-specific catalogue would undercut it; the paper should list the goals given.
Two parts with the same results on every contract case should count as one, keeping the cheaper, because structure-keyed duplicates fill the library with copies of one behaviour.

Order: the ten contracts; hardware cost measures; behavioural deduplication; the library on disk; the command.

Done when: at least delay, fan-out, add and the count-to-interval timer are solved from scratch and verified exhaustively, the library folder reloads them, and two runs with the same `--seed` produce the same library.

Read first: Claude Doc "Composing SN P Modules into Machines" (First parts to evolve, Build plan step 2); RESEARCH.md "Use cases and a practical path" item 3
