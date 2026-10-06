---
number: 128
title: Add timing primitives as specs
milestone: M5
labels:
  - area: tasks
  - area: modules
parent: m5-p3-verified-library
---

The primitives that recur in neuromorphic algorithms are mostly about when spikes arrive, which the interval and trigger ports already express, and evolution already solves timing parts.
We want specs for: first spike of k inputs, winner-take-all of k (one output per input, only the first fires), coincidence of k inputs within w steps, a refractory filter (pass a spike, drop any within r steps after), a delay line of d steps, and max and sort of k values given as spike times.

Its responsibilities are:
* Each spec with its parameters, cases, held-out cases and a latency bound
* Ties stated in each spec (equal arrival times), since every published design treats them differently
* Each spec run through `superopt` and the synthesis arm under `generic-if`

Ties should be specified, not left to the network, because a part that breaks ties one way in testing and another on hardware is wrong.
Parameters (k, w, r, d) should use the part-family form, because one contract per value does not scale.
The published size of each primitive, where a paper gives one, should be recorded beside the spec, because the comparison is the result.

Where: the spec type from M4; `SNP_Evolution/Evolution/Contracts/FirstParts.cs`, the part families from M3; RESEARCH.md "A verified spiking parts library, and search beyond evolution" for the published designs.

Done when:
- [ ] Each primitive is a spec with ties stated, for k up to 4 and w, r, d up to 8
- [ ] Each has a verified network under `generic-if`, saved to the library with its search method
- [ ] `dotnet test` green

Read first: RESEARCH.md "A verified spiking parts library, and search beyond evolution" (The gap)
