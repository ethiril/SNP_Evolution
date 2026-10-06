---
number: 69
title: M3 Part 4: Binary and parameterised parts
milestone: M3
labels:
  - Epic
---

Unary encodings cost 2^k steps or spikes for a k-bit number and no binary contract has been solved, and a library with one contract per delay k or add k grows without generalising.
We want a bit-serial adder found and composed into wider binary arithmetic, parts that come in families with a parameter, and a check for sub-compositions shared across promoted parts.

Its responsibilities are:
* A bit-serial adder, compared with the published streaming adder
* Parameterised part families
* Library compression over promoted recipes, as an investigation

Order: bit-serial adder; part families; library compression.

Done when: a bit-serial adder is in the library with its size next to Aimone et al.'s, one part family covers k up to 8 from parts proven for each k, and the compression investigation reports what it found.

Read first: RESEARCH.md "Use cases and a practical path" (item 1, Adders on Loihi 2), "Toward general synthesis" (items 6 and 7)
