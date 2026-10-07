---
number: 143
title: Superoptimise kept parts against their contracts
milestone: M4
labels:
  - area: search
  - area: cli
parent: m4-p2-superoptimisation
---

The library already holds correct parts (evolved delays and sequencers, and hand-built count parts that evolution has not found), and each has a contract to check against, but the only time a part is made smaller is the short shrink when it is first found, and nothing ever makes one faster.
We want a `superopt` command that takes kept or hand-built parts as seeds, searches for smaller and faster networks that still meet the part's contract, and saves an improvement to the library only when it is verified and proven at least as far as the seed.

Its responsibilities are:
* Seeds, picked by option: `--part FILE`, `--only NAMES` from the library folder, or `--hand-built` for the hand-built parts, each verified before the search starts
* The search: the shrink search's edits and admission, with a MAP-Elites map keyed by neurons and latency, scored by `HardwareCost` and then latency
* Proof: `BoundedCheck` on each new elite once, to at least the bound the seed was proven to
* Write-back: replacing the library's part when the new one is no worse on cost and latency and better on one, with its origin saying which part it was superoptimised from
* A report per seed: cost and latency before and after, the bound proven, and the evaluations spent

A seed that fails its contract should be refused with the failing case, because every descendant would be compared against a wrong reference.
A part should replace a kept one only when it is proven to at least the kept part's bound, because a smaller part that is less proven is not like for like.
Under the hardware profile, seeds and edits should stay inside the profile, and a profile part should be saved to the profile library only, because conforming can change behaviour and the two libraries hold different rule forms.
Latency should be an objective beside cost, because a part that is just as small but answers sooner makes every composition built from it faster.
Evaluations should be charged to the shared evaluation budget, because spec superoptimisation and the synthesis arm will be compared in the same units.
The command should be one `Command` with a registry line and a menu item on the library parts page, because every command is run the same way from the menu and the command line.
This command should be the one that later takes `--spec`, because a contract is already a spec with cases and a superoptimiser for specs only adds seeds and verification steps to it.

Where: new `Search/PartSuperoptimiser.cs`, new `Application/SuperoptService.cs`, new `Cli/Commands/SuperoptCommand.cs`; `Search/ShrinkSearch.cs`, `Search/PartSearch.cs`, `Search/Algorithms/MapElites.cs`, `Specs/Verification/Verifier.cs`, `Specs/Verification/BoundedCheck.cs`, `Specs/Parts/HardwareCost.cs`, `Specs/Parts/LibraryPart.cs`, `Specs/Parts/HandBuiltParts.cs`, `Specs/Parts/ReferenceParts.cs`, `Specs/Parts/ModuleLibrary.cs`, `Application/PartLibraries.cs`, `Storage/PartLibraryFiles.cs`, `Cli/Commands/CommandRegistry.cs`, `Cli/Menus/MainMenu.cs`.

Done when:
- [ ] `superopt --hand-built` returns, for at least three hand-built count parts, a part with fewer neurons or a lower latency than its seed, verified and proven to the seed's bound, on at least 8 of 10 seeds
- [ ] `superopt --only "delay 2,sequencer 2"` on `parts/` and `parts-profile/` never saves a part that is worse than the one kept
- [ ] A seed that fails its contract is refused with the failing case
- [ ] A saved part's origin names the part it was superoptimised from, and the `parts` command shows it
- [ ] RESEARCH.md has a table of every seed's cost, latency and bound before and after
- [ ] The README command table and menu list `superopt`
- [ ] `dotnet test` green

Read first: RESEARCH.md "Spec to verified circuit", "Proving contracts", README "Compile, then shrink"
