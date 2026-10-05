---
number: 7
title: Add port kinds and a Contract type
milestone: M1
labels:
  - area: modules
parent: m1-p1-contracts-ports-and-readouts
---

A module's only description of itself is `Module.Origin`, a string, so nothing can know that a module computes n+1.
We want a `PortKind` enum and a `Contract` record that describe what a part does, independent of any network.

Its responsibilities are:
* `PortKind`: `Interval` (two spikes n steps apart), `Count` (n spikes sent between start and done), `Trigger` (one spike), `Binary` (bit i of n is a spike or silence on step i after start, least significant bit first, fixed width w)
* `Port`: a name, a direction (in or out), a kind, and for `Binary` a width
* `Contract`: a name, the start port, one or more done ports (a zero test has a done per branch), the data ports, the test cases, and the maximum latency in steps
* `ContractCase`: the input value per data in-port, and the expected value per data out-port plus which done port should fire

Contracts should be plain immutable records with JSON round-tripping via Newtonsoft (as `Network` already uses), because the library on disk stores them next to each part.
A contract should validate itself (unique port names, exactly one start, at least one done, every case naming every data port) and say what is wrong, because a malformed contract would otherwise surface as a part that never scores.
Nothing should depend on how a network maps ports to neurons yet; that binding belongs to the part, not the contract.

Where: new folder `SNP_Evolution/Evolution/Contracts/` (namespace `SnpEvolution.Evolution.Contracts`); tests in `SNP_Evolution.Tests/Evolution/ContractTests.cs`.

Done when:
- [ ] `PortKind`, `Port`, `Contract`, `ContractCase` exist, commented in the style of `ITask.cs`
- [ ] A contract for a delay of k, an increment, and a zero test can be built and round-trip through JSON unchanged
- [ ] Validation rejects each malformed shape listed above with a message naming it
- [ ] `dotnet test` green

Read first: Claude Doc "Composing SN P Modules into Machines", section Ports and the start/done contract; RESEARCH.md "Use cases and a practical path", item 1 (binary encoding)
