---
number: 36
title: Benchmark evolved parts against published hand-built circuits
milestone: M1
labels:
  - area: research
  - kind: benchmark
parent: m1-p6-promotion-proposals-arithmetic
---

The paper needs a table of evolved against hand-designed, and the literature already has hand-built SN P arithmetic with known sizes.
We want each published circuit we can reconstruct entered as a reference (neurons, synapses, rules, steps, encoding), and the matching evolved part from our library measured the same way.

Targets:
- Zeng, Song, Zhang, Pan (2012): adder, subtracter, multiplier, divider, interval encoding
- Time-free SN P arithmetic (adder, subtracter, multiplier, divider)
- von Seeler et al. (2025): serial and parallel binary adders on Loihi 2 (neuron and synapse counts only, since the neuron model differs)
- Dong, Luo, Zhang (2023): evolved systems, as the prior evolved baseline

Sizes taken from a paper should cite the table or figure they came from, because several sources were only read as abstracts and must be opened before a number is used.
Rows should compare like with like (same encoding and operand range) or say why not, because a binary adder and a unary adder are not the same circuit.

Where: results table in RESEARCH.md; a reconstructed circuit, where possible, as a test network in `SNP_Evolution.Tests/` run through the same contract.

Done when:
- [ ] RESEARCH.md has the comparison table with a source for every hand-designed number
- [ ] Any reconstructed circuit passes its contract in a test

Read first: RESEARCH.md "Use cases and a practical path" (item 2 and its sources) and "Composing modules into machines" related work
