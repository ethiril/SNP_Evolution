---
number: 118
title: Write a discrete-time NIR profile with conformance tests
milestone: M5
labels:
  - area: export
  - area: research
parent: m5-p1-hardware-targets
---

NIR is defined in continuous time and leaves the step to each backend, so an integer circuit exported to it has no guaranteed meaning, and the mapping we use (dt = 1, threshold k as k - 0.5, recurrent edges through `Delay`) exists only in our notes and code.
We want that mapping written up as a discrete-time NIR profile, and a conformance suite: verified parts exported to NIR with the spike trace every backend must reproduce exactly.

Its responsibilities are:
* A document stating the profile: time step, threshold, reset, delays, weights, input timing, and what NIR graphs are inside it
* A conformance suite folder: per part, the NIR file, the input trains and the expected per-step spikes
* A runner that checks a NIR backend against the suite, starting with the norse path in `tools/snp_nir.py`

The profile should be stated so another NIR backend can implement it without our code, because the point is that parts move between simulators and chips.
Expected traces should come from our engine and be checked by co-simulation before they are written, because a conformance suite with a wrong answer fails correct backends.

Where: `SNP_Evolution/Export/NirExporter.cs`, `tools/snp_nir.py`, `SNP_Evolution/Cli/ExportCommands.cs`; the document in `docs/` or RESEARCH.md.

Done when:
- [ ] The profile document exists and every rule in it is covered by at least one conformance case
- [ ] The suite holds every verified profile part and passes under norse
- [ ] `dotnet test` green, with the norse tests skipped when Python or norse is missing

Read first: RESEARCH.md "Hardware profile and exporters" (Time step, Simulators), "A verified spiking parts library, and search beyond evolution"
