---
number: 131
title: Publish the library as a versioned release
milestone: M5
labels:
  - area: export
  - area: modules
parent: m5-p3-verified-library
---

`parts/` is a working folder this tool reads; nobody outside can tell from it what a part does, what was proven or how to run it on their simulator or chip.
We want `library release <dir>`: a versioned folder with one entry per part (spec, network, cost per target, proof report, Verilog, NIR) and a generated `INDEX.md` with a table of every part.

Its responsibilities are:
* The entry layout, reusing the result bundle from M4 Part 3
* The index: part, spec summary, size per target, bound proven, minimality result where there is one, search method, published size where there is one
* A version number and a changelog line per release
* Rebuilding every entry from the specs and checking it before writing

A release should be rebuilt and every part verified again before it is written, because a release is a promise about every part in it.
The index should be generated, never edited by hand, because hand edits drift from the parts.

Where: the result bundle from M4 Part 3; `SNP_Evolution/Storage/PartLibraryFiles.cs`, `SNP_Evolution/Cli/ExportCommands.cs`.

Done when:
- [ ] `library release` writes a folder whose index lists every part, and every entry's exports co-simulate
- [ ] A part that no longer verifies stops the release and is named
- [ ] `dotnet test` green

Read first: RESEARCH.md "A verified spiking parts library, and search beyond evolution", README "Exporting to hardware"
