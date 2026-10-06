---
number: 133
title: M5 Part 4: Applications
milestone: M5
labels:
  - Epic
---

A library is only shown useful by building something people build by hand today, with the parts it holds, and comparing the result.
We want two application builds from library parts: a delay-and-add pipeline of the kind used for radio burst dedispersion, and a verified spike-detection front end of the kind used in implants and audio chips.

Its responsibilities are:
* The delay-and-add pipeline under a chip target's delay limit
* The spike-detection front end: threshold, refractory filter and windowed count

Each build should be composed from library parts and then superoptimised as a whole, because that is the route the tool offers users.
Each should be compared with the hand design it replaces on size and steps, with the source of the hand design's numbers, because without the comparison it is a demo.

Order: delay-and-add pipeline; spike-detection front end.

Done when: both builds are verified, exported and in RESEARCH.md beside their hand designs.

Read first: RESEARCH.md "A verified spiking parts library, and search beyond evolution" (Where it matters in practice)
