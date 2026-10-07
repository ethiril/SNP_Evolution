---
number: 71
title: Add parameterised part families
milestone: M3
labels:
  - area: modules
parent: m3-p4-binary-and-parameterised-parts
---

Delay 1 to 4, add k and sequencer k are separate contracts, each evolved from scratch, and none says how to make delay 9 or sequencer 6.
We want part families: a contract with an integer parameter, and a way to get a part for any k from parts the library already has.

Its responsibilities are:
* A family form for contracts (delay k, sequencer k, add k, binary add of width k), naming the parameter and how the cases follow from it
* Building a member for a new k by composing members already in the library (two delays make a longer one; a sequencer of k chained to one of j makes one of k + j), then shrinking it
* Recording which members were evolved and which built

A built member should be verified and proven like any other part, because composing members is a composition and can fail.
A built member should be replaced by an evolved one only when the evolved one is cheaper, because the library already keeps the cheaper of two parts that read the same.
The family's members should be one entry per k in the library, under the family's name, because composition and part programs need a concrete part to wire.

Where: `Specs/Contracts/Contract.cs`, `Specs/Contracts/FirstParts.cs`, `Search/Modules/Promotion.cs`, `Specs/Parts/ModuleLibrary.cs`, `Storage/PartLibraryFiles.cs`.

Done when:
- [ ] Delay k and sequencer k are families, and members for k up to 8 are built from the library's members, verified and proven
- [ ] The library lists each member as built or evolved, with its size
- [ ] `dotnet test` green

Read first: RESEARCH.md "Toward general synthesis" item 7, README "Evolving library parts"
