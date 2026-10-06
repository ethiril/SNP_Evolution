---
number: 117
title: Add hardware targets with chip limits
milestone: M5
labels:
  - area: evolution
  - area: export
parent: m5-p1-hardware-targets
---

A profile network can still have more inputs per neuron, a longer delay or a larger weight than a chip allows, and nothing checks it.
We want named targets (`generic-if`, `loihi2`, `spinnaker2`, `xylo`) that each give the profile's rule form plus limits on weight range, fan-in, fan-out, longest delay and neuron count, with a check, a conform step and a cost per target.

Its responsibilities are:
* The target type: the limits, each with its source, and the profile it extends
* A check that names every limit a network breaks, and the neuron or synapse that breaks it
* Conforming edits for a search under a target, as the profile already has
* `HardwareCost` per target, so a delay longer than the target's limit costs the relays that would replace it
* A `--target` option on `verify`, `export` and the searches

Each chip limit should be cited from the vendor's documentation or a paper, and a limit with no source should be left unset rather than guessed, because a wrong limit is worse than none.
`--profile hardware` should mean `--target generic-if`, because there should be one notion of fitting hardware.

Where: `SNP_Evolution/Evolution/HardwareProfile.cs`, `SNP_Evolution/Evolution/HardwareCost.cs`, `SNP_Evolution/Cli/CommandLine.cs`, `SNP_Evolution/Cli/ExportCommands.cs`, `SNP_Evolution/Cli/VerifyCommand.cs`.

Done when:
- [ ] Each target lists its limits and sources with `targets`
- [ ] A part with a delay over Loihi 2's limit is refused by `verify --target loihi2`, naming the synapse
- [ ] `--profile hardware` and `--target generic-if` give the same results on the first parts
- [ ] `dotnet test` green

Read first: RESEARCH.md "A verified spiking parts library, and search beyond evolution" (Hardware limits), "Hardware profile and exporters"
