# Phase Plan

## Phase Sequence

1. Describe the intended execution order.
2. Call out the validator checkpoints between phases.
3. End with the final closure audit.

## Subbundle Dependency Map

```mermaid
gantt
title Replace with the real subbundle dependency and validation map
dateFormat  YYYY-MM-DD
section Foundations
Foundation subbundle :done, foundation, 2026-01-01, 1d
section Follow-on work
Dependent subbundle :after foundation, dependent, 1d
```

- Replace the placeholder map with the real subbundle order, prerequisites, and validation checkpoints.

## Critical Subbundles

- Identify the foundation subbundles whose correctness unlocks later phases.
- State the deeper validation required before dependent subbundles may continue.

## Phase Gates

- Gate after preparation: run the bundle validator and repair failures.
- Gate before each subbundle: confirm prerequisites are complete and still valid.
- Gate after each subbundle: capture proof, review screenshots, and decide whether downstream work may continue.
- Gate before closure: rerun validators, close raw notes, and reopen anything with weak proof.
