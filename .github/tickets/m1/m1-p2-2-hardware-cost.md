---
number: 14
title: Measure a part's hardware cost
milestone: M1
labels:
  - area: evolution
parent: m1-p2-evolve-first-parts
---

Choosing the smaller of two correct parts needs a measure of small, and neuron count alone hides most of what hardware pays for.
We want a `HardwareCost` record computed from a `Network`: neurons, synapses, distinct rules, total rules, the most spikes any neuron holds during the contract's cases (register width), and the total lasso table size of its rule conditions (`SpikeCondition.TailLength + Period` summed).

Register width should come from the simulation over the contract's cases, because it is a property of the run, not of the static network; the port readout's final counts are not enough, so record the maximum seen.
Cost should order lexicographically by neurons, then synapses, then rules, then register width, so ties are broken the same way everywhere.
The measure should be available as a MAP-Elites niche dimension and as a tie-breaker in the library, because the research notes call for hardware cost as an objective, not just a report.

Where: new `SNP_Evolution/Evolution/HardwareCost.cs`; `SNP_Evolution/Networks/SpikeCondition.cs` for lasso sizes; the simulation loop in `SNP_Evolution/Simulation/NetworkSimulation.cs` for the maximum held.

Done when:
- [ ] Cost of the two Part 1 reference parts is computed and asserted in a test
- [ ] Comparing two costs follows the stated order
- [ ] `dotnet test` green

Read first: RESEARCH.md "Use cases and a practical path", item 3
