---
number: 16
title: Save and load the part library as a folder
milestone: M1
labels:
  - area: modules
parent: m1-p2-evolve-first-parts
---

Parts evolved once should not be evolved again by every later run.
We want a library folder with one JSON file per part (network, contract, port binding, hardware cost, seed and run that found it) and a loader that rebuilds a `ModuleLibrary` from it.

Files should be named by contract and be stable across saves, because the folder will be committed and reviewed as a diff.
Loading should re-verify each part against its contract on the exhaustive engine and refuse a part that fails, naming it, because a hand-edited or stale file must not slip a broken part into composition.
Network JSON should reuse the existing format, because `NetworkFiles` already reads and writes it.

Where: `SNP_Evolution/Storage/NetworkFiles.cs` for the network format; new `SNP_Evolution/Storage/PartLibraryFiles.cs`; default folder `parts/` at the repo root, overridable by a setting in `SNP_Evolution/Cli/Settings.cs`.

Done when:
- [ ] Save then load gives a library equal to the original, part by part
- [ ] A part file edited to break its contract is refused on load with its name in the message
- [ ] `dotnet test` green

Read first: Claude Doc "Composing SN P Modules into Machines", Build plan step 2
