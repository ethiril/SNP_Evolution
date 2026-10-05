---
number: 8
title: Read the spikes of named port neurons
milestone: M1
labels:
  - area: simulation
parent: m1-p1-contracts-ports-and-readouts
---

Engines only report the output neuron (`Readout.Output`, `Readout.Halting`, `Readout.SpikeTrain`), so a part with several outputs and a done trigger cannot be scored.
We want a readout that records, for a chosen set of neurons, every step on which each fires and how many spikes it sends, plus each neuron's spike count when the run stops.

Its responsibilities are:
* A new `Readout.Ports` value and a way for a `Trial` to name the neurons it watches (1-based positions, as `Neuron.Connections` uses)
* `TrialResult` carrying per-neuron firing steps and spikes sent, per sampled run, and the final spike counts (for contract rule 3)
* A stop condition: the run ends a fixed number of steps after a watched done neuron first fires, or at `MaxSteps`

The readout should be implemented in `SequentialCpuEngine` and `ParallelCpuEngine`, and in `ExhaustiveCpuEngine` by following every computation, because contracts are verified exhaustively.
Where `ExhaustiveCpuEngine` merges configurations, the merge key should include whatever the readout still needs, so merged branches cannot lose a firing that would change a result; if that is not possible cheaply, the exhaustive engine should fall back to sampling and mark the result inexact, as it does for spike trains today.
`MetalEngine` should route `Readout.Ports` trials to a CPU engine for now, because GPU support is an optimisation and not needed for correctness.
Existing readouts should behave exactly as before.

Where: `SNP_Evolution/Simulation/ISimulationEngine.cs` (`Readout`, `Trial`, `TrialResult`), `SNP_Evolution/Simulation/NetworkSimulation.cs`, `SNP_Evolution/Simulation/SequentialCpuEngine.cs`, `SNP_Evolution/Simulation/ParallelCpuEngine.cs`, `SNP_Evolution/Simulation/ExhaustiveCpuEngine.cs`, `SNP_Evolution/Simulation/MetalEngine.cs`. Tests beside `SNP_Evolution.Tests/Simulation/SpikeTrainTests.cs`.

Done when:
- [ ] A three-neuron network watched on two neurons reports the right firing steps and spike counts on every CPU engine
- [ ] The exhaustive engine reports an exact result for a deterministic network and marks a too-wide one inexact
- [ ] Final spike counts are reported and match a hand trace
- [ ] Metal falls back without error; all existing engine tests pass unchanged

Read first: Claude Doc "Composing SN P Modules into Machines", sections Where the current module system falls short and Build plan step 1
