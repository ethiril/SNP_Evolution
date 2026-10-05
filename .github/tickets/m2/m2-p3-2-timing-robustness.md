---
number: 47
title: Score parts under random timing delays
milestone: M2
labels:
  - area: simulation
  - area: evolution
parent: m2-p3-streaming-and-timing-robustness
---

Asynchronous hardware jitters, and a part that only works under exact lockstep timing will break there.
We want a simulation option that delays each spike on each synapse by 0 to j extra steps at random, and a robustness score: the fraction of sampled jittered runs in which the part still meets its contract.

The robustness score should be reported for every library part and be available as a MAP-Elites dimension, because robustness is a property to select for, not only to report.
Jitter should be a sampled engine option and the exhaustive engine should refuse it, because the branching from every delay choice would explode the exhaustive search.

Where: `SNP_Evolution/Simulation/ISimulationEngine.cs` (`SimulationOptions`), `SNP_Evolution/Simulation/NetworkSimulation.cs`, `SNP_Evolution/Simulation/SequentialCpuEngine.cs`; the library summary from the `evolve-parts` command.

Done when:
- [ ] With j = 0 every result equals today's
- [ ] The first parts report a robustness score at j = 1 and j = 2 in the `evolve-parts` summary
- [ ] `dotnet test` green

Read first: RESEARCH.md "Use cases and a practical path", item 8
