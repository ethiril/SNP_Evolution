---
number: 27
title: Add the composition genome and its flattening
milestone: M1
labels:
  - area: modules
  - area: evolution
parent: m1-p5-composition-search
---

The algorithms evolve `Network`s directly, so there is no level at which a part is one gene.
We want a `Composition` record (part instances by library id, glue neurons, typed wires between named ports, which ports are the composition's own start, done and data ports) and a `Flatten` that produces a `Network` with every part neuron tagged by `ModuleTag`.

Its responsibilities are:
* `Composition` and `Flatten`
* `Recover`: rebuild a `Composition` from a flattened network whose tags are intact
* Random creation of a small composition from the library, for initial populations

`Recover(Flatten(c))` should equal `c`, because the algorithms work on networks and the composition has to survive a round trip through them.
Flattened networks should respect `GenomeSpace.MaxNeurons` only for glue, not for part bodies, because the cap exists to bound search, not verified parts.

Where: new `SNP_Evolution/Evolution/Modules/Composition.cs`; `SNP_Evolution/Evolution/Modules/ModuleEdits.cs` for tagging; `SNP_Evolution/Networks/Neuron.cs` (`ModuleTag`).

Done when:
- [ ] Round trip holds on a property test over random compositions
- [ ] The two-increment chain from Part 3 can be written as a `Composition` and flattens to the same network
- [ ] `dotnet test` green

Read first: Claude Doc "Composing SN P Modules into Machines", section The loop that keeps itself going (Compose)
