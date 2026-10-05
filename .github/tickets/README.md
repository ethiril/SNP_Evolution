# Tickets

Issues, labels and milestones as files, applied to GitHub with `tickets.py`
(copied from before-the-banners). A breakdown is written in one pass and
reviewed as a diff.

```
python3 .github/tickets/tickets.py plan        read-only: what apply would change
python3 .github/tickets/tickets.py plan -v     the same, with body diffs
python3 .github/tickets/tickets.py apply       create and update, writing new numbers back
python3 .github/tickets/tickets.py pull 12     write a file from an issue on GitHub
```

Apply is idempotent and additive: it never deletes an issue, label or milestone.

## A ticket

```markdown
---
title: Add a port readout
milestone: M1
labels:
  - area: simulation
parent: m1-p1-contracts-ports-and-readouts
---

The body, as markdown.
```

`parent` is an issue number or another ticket file's stem. Omit `number` to
create; apply writes it back. An absent key is left alone on GitHub.

## Writing a body

Every ticket must be picked up by an agent with no other input, so a body
carries what the agent needs:

1. **One line naming the need.** Why the ticket exists.
2. **`We want ...`**: the deliverable in one sentence.
3. **`Its responsibilities are:`** only where there are distinct ones.
4. **`should`** for each requirement, with the reason straight after it.
5. **`Where:`** the files and types to start from. Paths are relative to the
   solution folder `SNP_Evolution/`: `SNP_Evolution/...` is the app and
   `SNP_Evolution.Tests/...` the tests.
6. **`Done when:`** a checklist that a reviewer can tick without asking.
7. **`Read first:`** the spec sections, last line, no trailing period.

Rules: no em dashes; no references to issue numbers in bodies (the `parent`
key carries structure, and ordering lives in the epic); every ticket keeps
`dotnet test` green.

The spec documents:
- Claude Doc "Composing SN P Modules into Machines":
  https://claude.ai/code/artifact/c5af0396-5e34-46aa-a59d-798e552e681b
- `RESEARCH.md`, sections "Composing modules into machines" and "Use cases and
  a practical path"
