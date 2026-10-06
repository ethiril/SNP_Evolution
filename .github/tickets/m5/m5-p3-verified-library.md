---
number: 127
title: M5 Part 3: A verified parts library
milestone: M5
labels:
  - Epic
---

Spiking algorithm papers keep building the same primitives by hand (delay lines, first-spike and winner-take-all, comparators, counters, streaming adders, sorting by spike time), and none is minimal or machine-checked; the library here holds parts for our own tasks, not for that audience.
We want a library of those primitives, each with its spec, the smallest verified network found per target, a proof report and its exports, published in a form someone outside this codebase can use.

Its responsibilities are:
* Timing primitives as specs: first spike of k, winner-take-all of k, coincidence within w, refractory filter, delay line up to a target's limit, max and sort of k by spike time
* Binary primitives as specs: bit-serial add, subtract, compare and a k-bit counter, at widths 1 to 8
* State machines as stream specs, from a transition table
* A release format: per part its spec, network, cost per target, proof report and exports, and a generated index

Every primitive should be a spec, not a hand-built network, because the library's claim is that its parts were found and proven, not drawn.
Each part should be found by whichever search method gives the smallest verified network, and the release should name the method, because readers will ask how it was made.
Primitives should be judged under hardware targets first, because the audience runs integrate-and-fire chips.

Order: timing primitives; binary primitives; state machines; release.

Done when: the release folder holds every primitive at its widths with a proof report and NIR export for `generic-if`, and its index gives each part's size beside the published hand design where one exists.

Read first: RESEARCH.md "A verified spiking parts library, and search beyond evolution", "Evolved against hand-designed arithmetic"
