---
number: 45
title: M2 Part 3: Streaming tasks and timing robustness
milestone: M2
labels:
  - Epic
---

Every task so far starts once and finishes, and every score assumes perfect lockstep timing; controllers and asynchronous hardware have neither.
We want a family of streaming tasks (spikes in continuously, spikes out continuously, no start or done) and a scoring mode that adds random delays to test whether a part survives jitter.

Its responsibilities are:
* Streaming tasks: a debouncer and a rate detector first
* Scoring under random extra delays, following time-free SN P systems

The streaming tasks should be scored on windows of the output train rather than exact steps, because a controller is judged on behaviour over time, not per-step identity.

Order: streaming tasks; timing robustness.

Done when: a debouncer is evolved and passes its streaming task, and the first parts report a robustness score.

Read first: RESEARCH.md "Use cases and a practical path" (items 7 and 8)
