# Phase plan

## Subbundle Dependency Map

```mermaid
flowchart LR
    M00 --> M01 --> M02 --> M03 --> C1
    C1 --> M04 --> M05 --> M06 --> C2
    C2 --> M07 --> M08 --> M09 --> M10
```

## Critical Subbundles

`M01`, `M02`, `M03`, `M05`, `M06`, and `M08` are critical foundations. Reopening one invalidates every later checkpoint that reused its behavior, source anchor, or host proof.

## Phase Gates

- Phase A progresses only after C1 confirms MR-001 through MR-003 and no orphan descendant remains.
- Phase B progresses only after C2 confirms bounded MCP/Docker behavior and actual-host path/executable authority.
- Phase C freezes one immutable Windows/Linux candidate before M09; source changes after M08 reopen M08.

## Phase A — P0 compatibility and ownership

- M00: establish immutable source/dependency baseline.
- M01: make persisted process plans backward compatible and fail closed.
- M02: make FileTools dependency/capability provenance reproducible.
- M03: make process ownership include all descendants.
- C1: shared build/runtime checkpoint.

NO-GO after Phase A if any old-plan fixture cannot be classified safely, direct-source validation still depends on dirty state, or an orphan descendant survives.

## Phase B — protocol and authority hardening

- M04: local MCP peer-control and input bounds.
- M05: Docker strict recipes, local stack, future CI contract.
- M06: executable and workspace path authority.
- C2: Windows/Linux runtime portability checkpoint.

NO-GO after Phase B if malformed input can authorize a mutation, MCP can deadlock on ping, or a successful path/executable resolution is not host-realistic.

## Phase C — deterministic validation and final local candidate

- M07: build stamp, FQN catalog, invalidation ledger, canonical record cleanup.
- M08: integrated package-mode Windows/Linux gate and scheduled full suites.
- M09: freeze exact candidate and prepare colleague macOS handoff.
- M10: reconcile final records and issue decision.
