---
number: 20
title: Insert and wire parts by port type
milestone: M1
labels:
  - area: modules
parent: m1-p3-wiring-by-port-type
---

Random wiring is why chaining parts cannot be expressed.
We want an insertion that adds a part's body to a network and connects each of its in-ports to a compatible out-port already present (or to a network input), and its done port to a free start or to the network output, choosing among compatible options at random.

Its responsibilities are:
* A port-compatibility rule (kind, width, direction) in one place
* Finding the compatible ports a network already exposes, including those of parts inside it, using `ModuleTag` to know which neurons belong to which part instance
* Leaving a port unwired when nothing compatible exists, and saying so in the network's description

Insertion should never connect into a part's internal neurons, only its bound ports, because a wire into the middle of a verified part voids its verification.
Wiring should use the existing 1-based `Neuron.Connections`, because the engines and notation already understand it.

Where: `SNP_Evolution/Evolution/Modules/ModuleEdits.cs` (`Insert`, `InsertModule`); `SNP_Evolution/Networks/Neuron.cs` (`ModuleTag`). Tests in `SNP_Evolution.Tests/Evolution/ModuleTests.cs`.

Done when:
- [ ] Inserting a part into a network with one compatible count out-port wires to exactly that port
- [ ] No wire ever targets a non-port neuron of a part (property test over random networks)
- [ ] Uncontracted modules still insert as before
- [ ] `dotnet test` green

Read first: Claude Doc "Composing SN P Modules into Machines", Build plan step 3
