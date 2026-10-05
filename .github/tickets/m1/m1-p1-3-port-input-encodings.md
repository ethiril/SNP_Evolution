---
number: 9
title: Feed values to ports in each encoding
milestone: M1
labels:
  - area: simulation
parent: m1-p1-contracts-ports-and-readouts
---

`InputSpikes.Numbers` only knows the interval encoding, so a contract case cannot hand a count or a binary word to a part.
We want helpers that turn a contract case into the `InputSpikes` for a network whose input neurons are bound to ports.

Its responsibilities are:
* Start: one spike at a chosen step (default 0)
* Interval: two spikes n steps apart, the first at the start step
* Count: n spikes, one per step, starting at the start step
* Binary: on step start+i, a spike when bit i of n is 1, for i below the port width
* Trigger: one spike at the start step

The helpers should take the port order from the contract, because the binding from port to input neuron is the part's job and the input list must line up with it.
Encodings should be pure functions with no randomness, because the exhaustive engine needs the same input every time.

Where: `SNP_Evolution/Simulation/InputSpikes.cs` (static factories beside `Numbers`), or a new `SNP_Evolution/Evolution/Contracts/PortEncoding.cs` if it needs the contract types. Tests in `SNP_Evolution.Tests/Simulation/`.

Done when:
- [ ] Each encoding has a test showing the exact steps produced for n = 0, 1 and 5 (and width 4 for binary)
- [ ] A count of 0 sends no spikes and an interval of 0 is rejected with a clear message
- [ ] `dotnet test` green

Read first: Claude Doc "Composing SN P Modules into Machines", section Ports and the start/done contract; RESEARCH.md "Use cases and a practical path", item 1
