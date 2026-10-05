---
number: 38
title: M2 Part 1: Hardware profile and exporters
milestone: M2
labels:
  - Epic
  - area: export
---

Evolved networks live only inside this simulator, so the practical claim (automatically designed small spiking circuits for FPGAs and neuromorphic chips) has nothing to stand on.
We want networks exported to Verilog and, under a restricted rule profile, to the neuromorphic toolchains, with every export checked by co-simulation against our engine.

Its responsibilities are:
* A hardware profile that limits rules to threshold-and-reset forms
* A Verilog exporter for any network
* A NIR exporter for profile networks
* Co-simulation that requires identical spike traces

Every exporter should be checked by co-simulation, because an exporter that is plausibly right is worth nothing to a hardware claim.
The profile's cost in search difficulty should be measured, because it trades expressiveness for portability and the paper should say how much.

Order: hardware profile; Verilog exporter; NIR exporter.

Done when: a verified part from the library exports to Verilog and matches our engine bit for bit in co-simulation, and a profile part exports to NIR.

Read first: RESEARCH.md "Use cases and a practical path" (items 4 and 5, and the notes on NIR and Loihi 2)
