---
number: 44
title: Export parts to Uppaal timed automata
milestone: M2
labels:
  - area: export
  - kind: investigation
parent: m2-p2-contract-verification
---

Bounded checks stop at a bound, and a model checker can prove a property for all inputs or find why not.
We want an exporter from a part to an Uppaal model following the published translation of SN P systems with weighted synapses into timed safety automata, plus queries for contract rules 1, 2 and 3.

This ticket should start by reading the translation paper and writing down in RESEARCH.md which of our rule forms it covers, because the translation was made for a different variant and gaps must be known before code is written.
If the translation does not cover standard rules with consumption, the ticket should end with that finding and a proposal, because forcing it would give proofs about a different system.

Where: new `SNP_Evolution/Export/UppaalExporter.cs`; findings in RESEARCH.md.

Done when:
- [ ] RESEARCH.md says what the translation covers and what it does not
- [ ] If covered: the delay part is verified in Uppaal for rules 1 to 3, with the queries committed

Read first: RESEARCH.md "Use cases and a practical path", item 6 and its sources ("Modelling and verification of weighted spiking neural systems", TCS 2016; "Towards a general methodology for formal verification on spiking neural P systems", TCS 2024)
