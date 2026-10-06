---
number: 129
title: Add binary primitives as specs
milestone: M5
labels:
  - area: tasks
  - area: modules
parent: m5-p3-verified-library
---

Unary parts may be out of reach of integrate-and-fire neurons, and every practical arithmetic circuit is binary, so the library needs serial binary parts beyond the adder.
We want bit-serial add, subtract, compare (three trigger outs), and a k-bit counter, least significant bit first, as specs at widths 1 to 8.

Its responsibilities are:
* Specs for each, with the width as a family parameter
* Each run through `superopt` (seeded from the bit-serial adder where it applies) and the synthesis arm, under `generic-if` and one chip target
* Proofs for every input, since binary ports bound their values

A part that only works at the widths it was found at should be reported as such, because a serial part should not depend on width beyond its carry, and one that does is a lookup table.
Sizes should be compared with Aimone et al.'s streaming adder and Chen and Guo's binary circuits, because those are the published binary rows.

Where: the spec type from M4; the bit-serial adder from M3 Part 4; `SNP_Evolution/Evolution/Contracts/ArithmeticParts.cs`, `SNP_Evolution/Evolution/Contracts/PortEncoding.cs`.

Done when:
- [ ] Each primitive has a verified network proven for every input at widths 1 to 8 under `generic-if`
- [ ] Sizes are in RESEARCH.md beside the published binary rows
- [ ] `dotnet test` green

Read first: RESEARCH.md "Evolved against hand-designed arithmetic", "A verified spiking parts library, and search beyond evolution"
