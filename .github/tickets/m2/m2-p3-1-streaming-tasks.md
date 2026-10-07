---
number: 46
title: Add streaming tasks with sensor input and actuator output
milestone: M2
labels:
  - area: tasks
parent: m2-p3-streaming-and-timing-robustness
---

Published SN P controllers (enzymatic numerical SN P for wall-following robots) are streaming systems, and nothing in our task suite can score one.
We want a `StreamingTask : ITask` that feeds a long input spike train and scores the output spike train against a target behaviour, with two instances: a debouncer (output spikes once per burst of at least m input spikes within w steps) and a rate detector (output fires while the input rate is above r per w steps).

Its responsibilities are:
* Input trains generated from a seed, with several cases per task
* Scoring by windows: per window, does the output do the right thing, with one check per window
* Registration in the task suite

The task should use the existing `Readout.SpikeTrain`, because it already returns every firing step of the output neuron.
Inputs should be generated with a fixed seed per case, because the exhaustive engine needs the same input every time.

Where: new `Specs/Tasks/StreamingTask.cs`; `Specs/Tasks/SpikeTrains.cs` and `Search/Benchmarking/TaskSuite.cs`; `Simulation/InputSpikes.cs`.

Done when:
- [ ] Both tasks appear in `dotnet run -- tasks`
- [ ] A hand-built debouncer scores 1.0 and a network that copies its input scores below 0.5
- [ ] `dotnet test` green

Read first: RESEARCH.md "Use cases and a practical path", item 7
