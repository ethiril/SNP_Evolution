---
number: 70
title: Evolve a bit-serial adder
milestone: M3
labels:
  - area: modules
  - area: evolution
parent: m3-p4-binary-and-parameterised-parts
---

The binary contracts in `ArithmeticParts` (subtract, multiply, divide, compare) have never been solved, and there is no binary add, the first part any binary machine needs.
We want a binary add contract (two k-bit in-ports, a (k+1)-bit out-port, least significant bit first, as the `Binary` port kind already reads), solved by `evolve-parts` or by the compile route, and compared with the published streaming adder.

The contract should come in widths 1 to 4, because a 1-bit adder with carry is the step evolution can find, and a part that only works at one width is a lookup table.
The comparison should give neurons, synapses and steps against Aimone et al.'s 4 neurons and 9 synapses (as cited by von Seeler et al. 2025), because that is the baseline in our table.
The contract should be a family over width, because the binary primitives of the verified library in M5 are built on it.
If the profile run also solves it, the part should be exported to NIR and checked by co-simulation, because a bit-serial adder is the part that fits integrate-and-fire hardware.

Where: `Specs/Contracts/ArithmeticParts.cs`, `Specs/Contracts/Specification.cs`, `Specs/Contracts/PortEncoding.cs`, `Search/PartSearch.cs`; the published-circuit table in RESEARCH.md.

Done when:
- [ ] The binary add contracts and their specifications are in the catalogue
- [ ] `evolve-parts --only "add 4-bit"` over seeds 1 to 5, unrestricted and with `--profile hardware`, is reported in RESEARCH.md
- [ ] A solved adder is proven for every input (binary ports bound their values) and added to the published-circuit table
- [ ] `dotnet test` green

Read first: RESEARCH.md "Use cases and a practical path" (Adders on Loihi 2, item 1), "Toward general synthesis" item 6
