---
number: 135
title: Build a verified spike-detection front end
milestone: M5
labels:
  - area: research
  - kind: benchmark
parent: m5-p4-applications
---

Implant spike detectors and always-on audio front ends have fixed stages (threshold crossing, refractory period, count in a window) that must behave exactly under a microwatt budget, and their certification favours a proven circuit over a trained one.
We want a stream spec for that pipeline, built from library parts, superoptimised and verified, with a Verilog export and a proof report.

Its responsibilities are:
* The stream spec: input spikes from an upstream encoder, a refractory period r, a window w, and an output when the count in the window reaches t
* The build from library parts, then `superopt` on the whole
* The bundle, with what was and was not proven stated plainly

The report should be written for someone deciding whether to put the circuit in hardware, because that is the use the application stands for.
The proof should cover input trains up to a stated length and every train up to it, because a front end runs forever and the bound is the honest claim.

Where: the refractory filter and counter parts from M5 Part 3, the stream spec from M4 Part 1, the result bundle from M4 Part 3; results in RESEARCH.md.

Done when:
- [ ] The pipeline is verified for r, w, t up to 8 under `generic-if`, with a bundle holding Verilog and a proof report
- [ ] RESEARCH.md gives its size beside a published spike-detector pipeline's, if one gives sizes
- [ ] `dotnet test` green

Read first: RESEARCH.md "A verified spiking parts library, and search beyond evolution" (Where it matters in practice, item 3)
