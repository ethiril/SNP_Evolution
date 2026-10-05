---
number: 21
title: Add rewire, glue and swap mutations
milestone: M1
labels:
  - area: modules
  - area: evolution
parent: m1-p3-wiring-by-port-type
---

Once parts are wired by type, search needs moves that change the wiring rather than the parts.
We want three `IMutation`s: rewire one in-port to another compatible source; insert a glue neuron on one wire (a single neuron with a standard rule that passes or delays spikes, which plain neuron mutations can then change); swap one part instance for a library part with the same contract and lower hardware cost.

Swap should keep every wire on the same named ports, because two parts with one contract share port names even when their neuron indices differ.
Glue neurons should not carry a `ModuleTag`, because they are free for ordinary mutation and that is their purpose.
The mutations should be registered beside `InsertModule` and `DissolveModule` so the existing modular algorithm can use them by configuration.

Where: `SNP_Evolution/Evolution/Modules/ModuleEdits.cs`; registration where `InsertModule` is constructed (search for `new InsertModule`).

Done when:
- [ ] Each mutation has a test showing the network before and after on a two-part example
- [ ] A swap of a part for a cheaper equivalent leaves the network's contract results unchanged
- [ ] `dotnet test` green

Read first: Claude Doc "Composing SN P Modules into Machines", sections The loop that keeps itself going (Compose) and Build plan step 3
