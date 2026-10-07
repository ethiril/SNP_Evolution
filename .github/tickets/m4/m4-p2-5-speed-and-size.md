---
number: 144
title: Keep the smallest and the fastest part for each contract
milestone: M4
labels:
  - area: modules
  - area: search
parent: m4-p2-superoptimisation
---

The library keeps one part per contract and replaces it only with a cheaper one of the same behaviour, so a part that is larger but answers in fewer steps is thrown away, even though some compositions are limited by time and not by size.
We want the library to keep, for each contract, every part on the front of hardware cost against latency, with composition and superoptimisation able to ask for the smallest or the fastest.

Its responsibilities are:
* The front: a new part joins when no kept part is at least as cheap and at least as fast, and kept parts it beats on both leave
* Files: one file per part on the front, named so the smallest keeps today's file name
* Choosing: compositions take the smallest by default, and a `--prefer small|fast` setting picks otherwise
* Showing the front in the `parts` command

The smallest part should keep today's file name, because saved libraries, exports and tests name parts by it.
Parts on the front should be compared by the same `HardwareCost` order the shrink search uses, because two orders would disagree on which part is smaller.
The front should be small, because a part that is a step faster and a neuron larger is rarely worth a file; parts should join only when they are faster by at least one step or smaller by at least one neuron.

Where: `Specs/Parts/ModuleLibrary.cs`, `Specs/Parts/LibraryPart.cs`, `Specs/Parts/HardwareCost.cs`, `Storage/PartLibraryFiles.cs`, `Application/PartLibraries.cs`, `Application/LibraryView.cs`, `Search/CompositionSpace.cs`, `Application/Settings.cs`.

Done when:
- [ ] A library given a smaller-but-slower and a larger-but-faster part for one contract keeps both, and drops a third part beaten by one of them on both
- [ ] A library saved before this change loads unchanged, with each part as the smallest on its front
- [ ] `parts --only NAME` lists the front, and composition with `--prefer fast` uses the fastest part
- [ ] `dotnet test` green

Read first: RESEARCH.md "Spec to verified circuit", "A verified spiking parts library, and search beyond evolution"
