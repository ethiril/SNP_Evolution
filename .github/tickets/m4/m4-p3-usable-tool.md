---
number: 83
title: M4 Part 3: A usable tool
milestone: M4
labels:
  - Epic
---

A spec is a C# object, so only someone working in this codebase can give the tool a new algorithm, and what it returns is a network file that only this tool reads.
We want a spec file format an outside user can write, and a result bundle they can take to hardware and trust: the network, its exports, its cost and what was proven about it.

Its responsibilities are:
* A spec file format for functions, recurrences and relations, read by every spec command
* A result bundle and documentation for the spec-to-circuit route

The C# spec type should stay the internal form and the file should be read into it, because the tasks, the verifier and the searches take only that type.

Order: spec files; result bundle and documentation.

Done when: someone with only the README writes a spec file for an operation the catalogue does not have (such as max of two counts), runs `superopt` on it, and gets a bundle with a Verilog file and a proof report.

Read first: RESEARCH.md "Spec to verified circuit", README "Exporting to hardware"
