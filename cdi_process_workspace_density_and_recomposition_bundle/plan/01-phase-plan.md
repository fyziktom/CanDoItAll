# Phase Plan

## Phase Sequence

1. Prepare and validate the bundle before any code changes continue.
2. Land the workspace-density and viewport-width foundation first so later browser proof has the right target UI.
3. Build the shared CanvasLib recomposition and toolbar contract next.
4. Integrate the process-specific smart recomposition and persist it through the real process workflow.
5. Close with browser proof, managed SQLite verification, and bundle closure review.

## Subbundle Dependency Map

```mermaid
flowchart LR
    P[Prepared bundle] --> V1[Preparation validator passed]
    V1 --> S1[01 Workspace density and viewport width foundation]
    S1 --> G1[Gate 1: density proof and viewport proof]
    G1 --> S2[02 Shared CanvasLib recomposition engine and menu contract]
    S2 --> G2[Gate 2: shared math and toolbar proof]
    G2 --> S3[03 Process canvas integration and managed SQLite application]
    S3 --> G3[Gate 3: persisted process recomposition proof]
    G3 --> S4[04 Browser proof, database verification, and closure]
    S4 --> C[Closure validator and execution report]
```

## Critical Subbundles

- `subbundles/01-workspace-density-and-viewport-width-foundation`
  - This is the UI foundation for the space-saving request.
  - Weak proof here would invalidate later browser conclusions because the page chrome would still be wasting space.
- `subbundles/02-shared-canvaslib-recomposition-engine-and-menu-contract`
  - This is the shared architectural foundation.
  - Weak proof here would let process-specific code paper over a broken shared contract and create future duplication.
- `subbundles/03-process-canvas-integration-and-managed-sqlite-application`
  - This is the functional core of the request.
  - Weak proof here would mean the algorithm might look plausible in memory but fail to persist or fail on real data.

## Phase Gates

- Gate after preparation:
  - Run `validate_bundle.py --stage prepared`.
  - Repair any missing traceability, subbundle gates, or proof gaps before code execution continues.
- Gate after subbundle 01:
  - Summary-tile badge mode is implemented and browser evidence shows better width use and less wasted height on `/processes`.
- Gate after subbundle 02:
  - Shared recomposition math and toolbar menu plumbing compile and have focused automated proof.
- Gate after subbundle 03:
  - A real process definition in the managed SQLite workspace can run persisted recomposition through the product path and reopen with clearer positions.
- Gate before closure:
  - Rerun targeted tests, collect final screenshots, inspect the managed SQLite persistence artifact, update the execution report, and only then mark the bundle completed.
