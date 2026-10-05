---
number: 35
title: Add contract tasks for subtraction, multiplication, division and comparison
milestone: M1
labels:
  - area: tasks
parent: m1-p6-promotion-proposals-arithmetic
---

Arithmetic is the longer aim, and each operation tests whether the library compounds: multiplication should reuse an add loop, division a subtract loop.
We want contracts for n1 - n2 (n1 >= n2), n1 x n2, n1 div n2 with remainder, and n1 < n2 (two done branches), in count encoding first and binary encoding second, registered in the task suite so the command line can run them.

Cases should include zero operands and one larger case, because those are where hand-built SN P arithmetic usually breaks.
The binary versions should use fixed widths of 4 and 8 bits, because the published Loihi 2 and FPGA adders report at those scales.

Where: `SNP_Evolution/Evolution/Contracts/` beside the first parts; `SNP_Evolution/Evolution/Tasks/TaskSuite.cs`.

Done when:
- [ ] All eight contracts validate and appear in `dotnet run -- tasks`
- [ ] A composition run for n1 x n2 reports whether it used a promoted add-loop part
- [ ] `dotnet test` green

Read first: Claude Doc "Composing SN P Modules into Machines", Build plan step 6; RESEARCH.md "Use cases and a practical path" item 1
