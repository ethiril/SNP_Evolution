---
number: 115
title: M5 Part 1: Hardware targets
milestone: M5
labels:
  - Epic
---

The hardware profile only fixes the rule form (one threshold rule, axonal delays, no initial spikes), so a part can pass it and still not fit a chip: real chips limit synapse weights, fan-in, fan-out and the longest delay.
We want named hardware targets that state those limits, synapse weights in the model so the profile can use them, and a written discrete-time NIR profile with the library's parts as its conformance tests.

Its responsibilities are:
* Integer synapse weights in the network model, every engine and every exporter
* Targets: a generic integrate-and-fire target and chip targets with their limits, checked and costed
* A discrete-time NIR profile and a conformance suite built from verified parts

Targets should extend the hardware profile rather than sit beside it, because two notions of "fits the hardware" would drift.
Every limit on a chip target should cite its source, because a wrong limit makes a part look deployable when it is not.

Order: synapse weights; targets; NIR profile and conformance.

Done when: `verify` and `superopt` accept `--target`, a part over a target's fan-in or delay limit is refused with the limit it breaks, and the conformance suite runs under norse.

Read first: RESEARCH.md "A verified spiking parts library, and search beyond evolution", "Hardware profile and exporters"
