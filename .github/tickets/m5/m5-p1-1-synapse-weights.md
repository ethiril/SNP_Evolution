---
number: 116
title: Add integer synapse weights
milestone: M5
labels:
  - area: simulation
  - area: export
parent: m5-p1-hardware-targets
---

Every synapse carries what its neuron produces, so a neuron that should count one input twice needs two relays, and integrate-and-fire chips, whose synapses all have integer weights, are used at a fraction of what they can do.
We want an optional integer weight on each synapse, 1 by default, that multiplies the spikes delivered along it, supported by every engine, the bounded check and every exporter.

Its responsibilities are:
* The weight on the synapse type, saved and loaded, with networks without weights loading as weight 1
* Delivery in every engine: the deterministic simulation, the exhaustive engine and the Metal engine
* Exports: Verilog, NIR (as the recurrent weight), Uppaal
* Mutations that change a weight, on only when weights are enabled

Weights should be non-negative unless a target allows inhibition, because standard SN P has no inhibition and a negative weight changes what the model can compute.
Weights should be off by default in every search, because results so far were found without them and must stay reproducible.
The step semantics should change in one place only, because each engine and exporter repeating the delivery rule is how they drift apart.

Where: `SNP_Evolution/Networks/` (synapse and network types), `SNP_Evolution/Simulation/NetworkSimulation.cs`, `SNP_Evolution/Simulation/ExhaustiveCpuEngine.cs`, `SNP_Evolution/Simulation/Metal/`, `SNP_Evolution/Export/VerilogExporter.cs`, `SNP_Evolution/Export/NirExporter.cs`, `SNP_Evolution/Export/UppaalDeclarations.cs`, `SNP_Evolution/Evolution/Operators/StructuralMutations.cs`, `SNP_Evolution/Storage/`.

Done when:
- [ ] A network with weights gives identical traces on every engine and under Verilog and NIR co-simulation, over 25 random weighted networks
- [ ] Saved networks without weights load and score as before
- [ ] `dotnet test` green

Read first: RESEARCH.md "Hardware profile and exporters", "A verified spiking parts library, and search beyond evolution" (Hardware limits)
