---
number: 19
title: Expose typed ports on library parts
milestone: M1
labels:
  - area: modules
parent: m1-p3-wiring-by-port-type
---

A `Cut` only lists anonymous input and output neuron indices, which says nothing about which is start, done or a data port.
We want a contract part in the library to carry its `PortBinding`: for each contract port, the neuron index in the part's body that receives (in-ports) or sends (out-ports) on it.

A binding should be checked when the part is added (each port bound once, bound neurons exist, in-ports have no other outside senders), because composition trusts it blindly afterwards.
`Cut.Inputs` and `Cut.Outputs` should still be filled for contract parts, derived from the binding, so the code paths that read them keep working.

Where: `SNP_Evolution/Evolution/Modules/ModuleLibrary.cs` (`Module`), `SNP_Evolution/Evolution/Modules/ModuleCuts.cs` (`Cut`); the `PortBinding` type from the contract task work in `SNP_Evolution/Evolution/Contracts/`.

Done when:
- [ ] A part loaded from the library folder reports its ports by name with neuron indices
- [ ] A binding that names a missing neuron or binds a port twice is rejected
- [ ] `dotnet test` green

Read first: Claude Doc "Composing SN P Modules into Machines", section Ports and the start/done contract
