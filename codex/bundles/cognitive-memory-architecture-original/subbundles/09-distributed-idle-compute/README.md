# 09 Distributed Idle Compute

## Status

- Ready after deterministic consolidation exists.

## Objective

- Allow trusted LAN devices to process deterministic memory jobs without directly mutating authoritative memory or projection state.

## Covered Inputs

- Requirements FR-019, FR-020, FR-021, FR-022, NFR-001, NFR-012, and NFR-013.
- Distributed idle compute architecture and operational modes.

## Prerequisites

- `06-consolidation-engine` must provide deterministic job inputs and idempotent acceptance.
- `04-memory-taxonomy-and-projections` must define authoritative write paths.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture\architecture\09-distributed-idle-compute.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture\architecture\13-operational-modes-and-scale.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture\contracts\csharp\DistributedComputeContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Automation\CanDoItAll.Modules.Automation.csproj
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.SchedulerPlanner\CanDoItAll.Modules.SchedulerPlanner.csproj

## Deliverables

- Job package and lease model.
- Worker registration and capability model.
- Output verification and authoritative acceptance flow.
- Failure, stale lease, incompatible version, and rejected output behavior.

## Dependency Impact

- Coordinator remains authoritative.
- Workers receive bounded immutable inputs and return signed/hashed outputs.
- Projection updates happen only after coordinator validation.

## Validation Depth

- Unit tests for lease expiry and idempotency.
- Integration tests for stale, duplicate, tampered, and incompatible outputs.

## Implementation Steps

- Define distributed job package format.
- Add coordinator lease and claim flow.
- Add worker output verification.
- Accept outputs through the same consolidation/projection services used locally.

## Do Not Do

- Do not give workers direct database or Qdrant write access.
- Do not accept output without input hash, output hash, algorithm version, and lease token.
- Do not use distributed compute for interactive recall.

## Acceptance Checklist

- Worker outputs can be rejected predictably.
- Coordinator can requeue expired jobs.
- Accepted output is indistinguishable from local deterministic job output except for worker provenance.

## Proof Required

- Lease and rejection tests.
- Integration evidence for one accepted and one rejected worker output.
- Metrics/log evidence for claim, accept, reject, and expiry counts.

## Browser Validation Logging

- Browser proof is required only if worker/job health UI is included.
- If included, record route and viewport evidence in `reviews/01-execution-report.md`.

## Progression Gate

- Proceed to cross-project memory only after distributed outputs cannot corrupt authoritative state.

## Suggested Agent Prompt

- Implement distributed idle compute as deterministic delegated work with coordinator-controlled acceptance and no direct worker writes.
