---
number: 85
title: Write a result bundle and document the spec route
milestone: M4
labels:
  - area: export
  - area: cli
parent: m4-p3-usable-tool
---

A superoptimised network is only useful outside this tool if it comes with its hardware exports and a plain statement of what was checked, and today those come from separate commands with no record tying them together.
We want each spec run to write a bundle folder holding the spec, the network, its Verilog and NIR exports with their co-simulation results, its hardware cost, and a proof report, plus a README section on the spec route.

Its responsibilities are:
* The bundle: `spec`, `network.json`, `network.v` and testbench, `network.nir` when it fits the profile, `cost.json`, and `report.md`
* The report: the steps passed, the training and held-out ranges, the bound proven, the counterexamples found during the run, the seed it started from and the cost against the seed
* A README section, "From a spec to a verified circuit", with one worked example from spec file to bundle

The report should state what was not checked as plainly as what was, because a user taking a part to hardware needs to know whether it was proven or only tested.
Exports should only be written when their co-simulation passes, and the report should say why one is missing, because a Verilog file that disagrees with the network is worse than none.

Where: `Cli/Commands/ExportCommands.cs`, `Export/VerilogExporter.cs`, `Export/VerilogTestbench.cs`, `Export/NirExporter.cs`, `Storage/NetworkFiles.cs`, the superoptimiser and verifier from M4 Part 2; `README.md`.

Done when:
- [ ] `superopt --spec add --profile --out <dir>` writes a bundle whose Verilog and NIR co-simulate against the network, and whose report gives the bound proven
- [ ] A bundle for a network outside the profile has no NIR file and the report says why
- [ ] The README worked example runs as written
- [ ] `dotnet test` green

Read first: RESEARCH.md "Hardware profile and exporters", README "Exporting to hardware"
