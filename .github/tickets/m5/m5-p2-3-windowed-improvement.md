---
number: 122
title: Improve correct networks one window at a time
milestone: M5
labels:
  - area: search
parent: m5-p2-search-beyond-evolution
---

Exact synthesis only scales to a handful of neurons, but a compiled or composed network is tens of neurons made of parts whose boundaries are known.
We want a superopt method that cuts a window out of a correct network (one part copy, or a few connected copies and their glue), takes the window's behaviour at its ports as a spec, resynthesises it smaller, splices it back and verifies the whole network.

Its responsibilities are:
* Choosing windows: part copies from the composition, then pairs of neighbours, largest saving first
* The window's spec: its port traces on every case of the whole network, plus the window's own contract when it is a single part
* Splicing and verifying the whole network after each replacement
* Reporting each replacement: window, size before and after, and the verifier step reached

A replacement should be kept only when the whole network still passes the verifier, because a window can be correct on its observed traces and still break timing the rest of the network relies on.
Windows should follow start and done boundaries where they exist, because those are the boundaries where timing is already contracted.

Where: the exact synthesis from this epic; `SNP_Evolution/Evolution/Modules/PartWiring.cs`, `SNP_Evolution/Evolution/Modules/Composition.cs`; the superoptimiser from M4.

Done when:
- [ ] On the compiled add and multiply networks and the hand-built Fibonacci composition, windowed improvement returns a smaller verified network, with each replacement reported
- [ ] A replacement that breaks the whole network is rejected and logged
- [ ] `dotnet test` green

Read first: RESEARCH.md "A verified spiking parts library, and search beyond evolution" (Windowed local improvement)
