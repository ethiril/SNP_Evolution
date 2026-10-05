---
number: 13
title: Define the ten first part contracts
milestone: M1
labels:
  - area: modules
  - area: tasks
parent: m1-p2-evolve-first-parts
---

Evolution needs concrete goals for its first library.
We want the ten contracts below as a static catalogue, each with test cases for n from 0 to 8 plus one larger value (12 or so) to catch parts that only memorised small cases.

| Part | Ports besides start and done | Contract |
|---|---|---|
| Delay k | none | done fires k steps after start (k = 1..4, one contract each) |
| Fan-out | count in; count out x2 | both outputs carry n |
| Increment | count in; count out | output carries n + 1 |
| Double | count in; count out | output carries 2n |
| Add | count in x2; count out | output carries n1 + n2 (cases cover pairs up to 6 + 6) |
| Interval to count | interval in; count out | output carries n |
| Count to interval (timer) | count in; interval out | two output spikes n steps apart |
| Register | count in; count out | holds n until started again, then drains it |
| Zero test | count in; done-zero, done-nonzero | the right branch fires, the other never |
| Sequencer | done out xk | fires its outputs in order, each one step after the previous (k = 2, 3) |

Each contract should set its maximum latency generously (a few steps per unit of the largest case), because a tight bound would rule out slow but correct parts that MAP-Elites can later shrink.
The catalogue should be one place the paper's table can be generated from, because the paper must state exactly which goals were given.

Where: new `SNP_Evolution/Evolution/Contracts/FirstParts.cs`; tests check each contract validates and that the hand-built register and delay from Part 1 pass theirs.

Done when:
- [ ] All ten (with the k variants) exist and validate
- [ ] A test prints the catalogue as a markdown table matching the one above
- [ ] `dotnet test` green

Read first: Claude Doc "Composing SN P Modules into Machines", section First parts to evolve
