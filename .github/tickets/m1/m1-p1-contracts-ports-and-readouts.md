---
number: 6
title: M1 Part 1: Contracts, ports and readouts
milestone: M1
labels:
  - Epic
---

A module cannot say what it computes, and nothing can read a result from any neuron but the single output. Composing parts on purpose needs both.
We want every part to carry a contract: typed data ports, a start trigger, a done trigger, test cases and a maximum latency, scored by a task the existing algorithms already run.

Its responsibilities are:
* Port encodings: interval, count, trigger and binary
* Readouts that observe named port neurons rather than only the output neuron
* Input helpers that feed a value to a port in each encoding
* A contract task that scores the four contract rules, one check per case and rule
* Hand-built reference parts that prove the scoring right and wrong

Every part should have one start input and one done output, because SN P systems step in lockstep and a part one step slower breaks a fixed-latency chain; with triggers a smaller part with a different latency can still be swapped in.
Binary should be a port kind from the start, because interval and count are unary and a k-bit number costs up to 2^k steps, which rules out any practical arithmetic.
The contract rules should be checked on the exhaustive engine over every computation, because a sampled pass can be lucky.

The four contract rules, for every input in the test set:
1. Nothing is sent on any output before a spike reaches start.
2. After start, the data outputs carry the right values and done fires exactly once.
3. After done, every neuron holds the spikes it started with, so the part can be started again.
4. Done fires within the recorded maximum latency.

Order: port kinds and contracts; port readouts; port input encodings; the contract task; the reference parts.

Done when: a hand-built register and a hand-built delay pass every check on the exhaustive engine, a broken copy of each fails the right check, and `dotnet test` stays green.

Read first: Claude Doc "Composing SN P Modules into Machines" (sections Ports and the start/done contract, Build plan step 1); RESEARCH.md "Composing modules into machines" and "Use cases and a practical path" item 1
