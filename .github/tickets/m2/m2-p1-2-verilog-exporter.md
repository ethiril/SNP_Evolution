---
number: 40
title: Export networks to Verilog and co-simulate
milestone: M2
labels:
  - area: export
parent: m2-p1-hardware-profile-and-exporters
---

Published SN P arithmetic already runs on FPGAs, by hand; an exporter would let any evolved part do the same.
We want an `export-verilog` command that writes a synthesisable Verilog module per network: one neuron module built from a spike counter, the rule conditions as lasso lookups (`SpikeCondition` tail table plus period), a delay counter and a closed flag, wired by the network's synapses, with input and port neurons as module ports.

Its responsibilities are:
* The generator, deterministic and readable
* A testbench generator that drives the contract's cases and dumps per-step spikes
* A co-simulation test that runs Icarus Verilog (`iverilog`) and compares traces with `SequentialCpuEngine`

The exporter should handle deterministic networks only and refuse others with a message, because hardware has no free choice between rules and a choice would have to be made by a policy the paper would need to defend.
Counter widths should come from `HardwareCost` register width, because guessing a width risks silent overflow.
The co-simulation test should skip with a clear message when `iverilog` is not installed, because CI may not have it.

Where: new `SNP_Evolution/Export/VerilogExporter.cs`; `SNP_Evolution/Networks/SpikeCondition.cs` (`Accepts`, `TailLength`, `Period`); command in `SNP_Evolution/Cli/CommandLine.cs`; test in `SNP_Evolution.Tests/Export/`.

Done when:
- [ ] The delay, register and add parts export and match our engine step for step on every contract case under `iverilog`
- [ ] A nondeterministic network is refused with the reason
- [ ] `README.md` documents the command and the `brew install icarus-verilog` prerequisite
- [ ] `dotnet test` green

Read first: RESEARCH.md "Use cases and a practical path", item 5
