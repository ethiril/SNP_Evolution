---
number: 134
title: Build a delay-and-add pipeline from library parts
milestone: M5
labels:
  - area: research
  - kind: benchmark
parent: m5-p4-applications
---

Dedispersion, beamforming and filterbanks on neuromorphic chips are trees of delays and sums, designed by hand (Magro 2026 on SpiNNaker2), and a chip's longest delay limits how they can be built.
We want a spec for a delay-and-add tree (c input channels, a delay per channel, a sum over a window, a threshold for detection), built from library parts under a chip target, superoptimised, verified and compared with the hand design's size.

Its responsibilities are:
* The spec, with channels, delays and threshold as parameters, and inputs as spike trains
* A build for small sizes (c up to 8, delays up to twice the target's limit, so long delays must be chained)
* A comparison with the hand design's neuron and synapse counts, from its paper or derived from its description

Delays past the target's limit should be built from the library's delay line parts, because that is the constraint the hand designs work around.
The comparison should say how the hand design's numbers were obtained, because the paper may only describe the structure.

Where: the delay line and add parts from M5 Part 3, `superopt`, the targets from M5 Part 1; results in RESEARCH.md.

Done when:
- [ ] The pipeline for c = 4 and c = 8 is verified under `loihi2` or `spinnaker2` and exported to NIR
- [ ] RESEARCH.md gives its size and steps beside the hand design's
- [ ] `dotnet test` green

Read first: RESEARCH.md "A verified spiking parts library, and search beyond evolution" (Where it matters in practice, item 1)
