---
number: 123
title: Find and apply rewrite rules proven on the exhaustive engine
milestone: M5
labels:
  - area: search
  - kind: investigation
parent: m5-p2-search-beyond-evolution
---

Shrink edits are random and every result has to be checked again, while a rewrite proven once (two relays in a row are one delay, two neurons with the same inputs and rule are one) is safe wherever it applies.
We want to know whether a set of local rewrite rules over networks can be found, proven, and used to shrink networks without checking each result, and a prototype if so.

Its responsibilities are:
* Candidate rules: hand-written ones, and ones found by enumerating small pattern pairs that the exhaustive engine shows behave the same at their boundary, as Enumo does
* A proof per rule, with the conditions under which it holds (no other synapse into the middle neuron, no initial spikes)
* Applying rules greedily, and, if the gain justifies it, with an e-graph and extraction by `HardwareCost`
* The written result: rules found, how much they shrink compiled networks, and how that compares with shrinking

A rule should only apply where its conditions hold in the whole network, because a rewrite that ignores a side input changes behaviour.
Results should still go through the verifier while the method is new, because the rules are only trusted once their results agree with it.

Where: `SNP_Evolution/Compilation/ShrinkRun.cs`, `SNP_Evolution/Simulation/ExhaustiveCpuEngine.cs`, `SNP_Evolution/Networks/`; results in RESEARCH.md.

Done when:
- [ ] RESEARCH.md lists the rules found, their conditions, and the shrink they give on the compiled add, multiply and Fibonacci networks next to `ShrinkRun`'s
- [ ] Every network shrunk by rules passes the verifier
- [ ] It ends with a build-or-leave recommendation for e-graphs
- [ ] `dotnet test` green

Read first: RESEARCH.md "A verified spiking parts library, and search beyond evolution" (Rewrite rules and e-graphs), README "Compile, then shrink"
