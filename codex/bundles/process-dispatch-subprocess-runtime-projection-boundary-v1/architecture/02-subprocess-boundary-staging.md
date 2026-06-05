# Subprocess Boundary Staging

## Stage 1: facts/rules only

Extract terminal status mapping, transition reason, start/block transition request builders, and capability-gap facts.

## Stage 2: artifact projection planning

Extract projection key, path, markdown, lineage, provenance, review summary, and sensitivity rules.

## Stage 3: side-effect coordinators

Extract explicit coordinators for:

- subprocess run observation/start through `ProcessesService`;
- projection gap journal writes;
- parent-scoped markdown file writes;
- parent artifact record and artifact-recorded journal writes.

## Stage 4: facade integration

Make `HandleSubprocessDispatchAsync` small or move it to a dedicated partial/facade. The main dispatch loop should delegate the subprocess branch.
