---
number: 24
title: Decide how a Fibonacci round lasts exactly B steps
milestone: M1
labels:
  - area: research
  - kind: investigation
parent: m1-p4-fibonacci-by-hand
---

The swap and the output spike take steps of their own, so a round may last B + c steps rather than B, and the gaps would come out shifted by c.
We want the answer written down before the network is built: either the swap overlaps with the drain, or B' starts c lower, or the output timer is offset by c.

The investigation should simulate small candidate wirings by hand or in a scratch test rather than argue on paper, because off-by-one timing in lockstep systems is easy to get wrong in the head.
The answer should be written to RESEARCH.md under "Composing modules into machines", replacing the open question, because the paper will cite the representation's limits.

Where: scratch tests under `SNP_Evolution.Tests/Evolution/`; the parts from the library folder.

Done when:
- [ ] RESEARCH.md says which option works and shows the step trace of one round for B = 3
- [ ] The open question is removed from the spec doc's open questions (or marked answered)

Read first: Claude Doc "Composing SN P Modules into Machines", section Fibonacci as a register program (open question)
