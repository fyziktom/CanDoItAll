# Phase Plan

## Execution order
1. SB01: Baseline reconciliation and previous blocker closure.
2. SB02: UI project/project-structure launch-to-completed-run proof.
3. SB03: Representative template automation regression hardening.
4. SB04: Runtime-host operator readback closure.
5. SB05: Scheduler/workflow process-owned lifecycle closure.
6. SB06: Final release matrix, live smoke classification, and merge decision.

## Subbundle Dependency Map

```mermaid
graph TD
  SB01[SB01 baseline + blocker closure] --> SB02[SB02 UI launch-to-completion]
  SB02 --> SB03[SB03 representative automation matrix]
  SB03 --> SB04[SB04 runtime-host operator readback]
  SB04 --> SB05[SB05 scheduler/workflow lifecycle]
  SB05 --> SB06[SB06 final release decision]
```

## Critical subbundles
All six subbundles are critical. They are intentionally large implementation areas rather than micro-edits.

## Code-first rule
Final closure is blocked unless:

```text
(src + tests changed lines) >= 5 × codex/bundles changed lines
```

Docs do not count as implementation. The ratio baseline must be an explicit SHA captured at the start of execution, not `HEAD`, branch names, or an older bundle baseline.

## Proof discipline
Each subbundle should add concise evidence only. Do not create large proof directory trees. Prefer focused test names, source assertions, and one short manifest update per critical phase.
