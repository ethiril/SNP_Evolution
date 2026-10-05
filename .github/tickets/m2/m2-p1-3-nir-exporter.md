---
number: 41
title: Export profile networks to NIR
milestone: M2
labels:
  - area: export
parent: m2-p1-hardware-profile-and-exporters
---

NIR is the shared representation that runs on 7 neuromorphic simulators and 4 digital hardware platforms, so one exporter reaches Loihi 2, SpiNNaker 2 and the rest.
We want an `export-nir` command that writes a hardware-profile network as a NIR graph of integrate-and-fire nodes with integer thresholds, reset to zero, unit weights and the rule delays as delays.

The exporter should refuse networks outside the hardware profile and say which rule breaks it, because the mapping is only exact under the profile.
The mapping from discrete SN P steps to NIR's continuous-time primitives should be written down in a comment and in RESEARCH.md, because NIR abstracts away discretisation and the choice of time step is part of the claim.
Co-simulation should use a small Python script with the `nir` and `snntorch` or `norse` packages, run optionally from the tests, because no .NET NIR runtime exists.

Where: new `SNP_Evolution/Export/NirExporter.cs` (NIR files are HDF5; writing a JSON intermediate plus a Python converter in `tools/` is acceptable); command in `SNP_Evolution/Cli/CommandLine.cs`.

Done when:
- [ ] A profile delay and a profile add export, load in the `nir` Python package, and match our traces in one simulator
- [ ] The time-step mapping is written down
- [ ] `dotnet test` green

Read first: RESEARCH.md "Use cases and a practical path" (the NIR and Loihi 2 notes, item 5); Pedersen et al. (2024), https://arxiv.org/abs/2311.14641
