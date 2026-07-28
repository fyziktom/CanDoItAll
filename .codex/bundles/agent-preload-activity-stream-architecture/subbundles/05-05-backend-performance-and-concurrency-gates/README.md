# 05 Backend Performance and Concurrency Gates

## Status

- `Completed`
- Gate: `A5 Go with three P2 follow-ups`

## Objective

- Measure the completed backend against the locked baseline, prove concurrency/query/storage correctness, and issue the hard go/no-go decision for UI work.

## Success Criteria

- Time to first activity is immediate and independent of canonical run persistence.
- Cold/warm acceptance-to-runtime-start and operation counts improve materially or remain explicitly justified where external costs dominate.
- No shared-DbContext/store-write/capability-composition parallelism exists.
- Provider/process EF queries are no-tracking/projected/bounded where read-only and avoid N+1/client-evaluation hazards.
- Architecture/dependency, concurrency, disposal, cancellation, file-lock, and existing behavior tests pass.

## Covered Inputs

- R08-R09, performance/EF optimization, backend-first measured gate, general functionality review.

## Prerequisites

- SB04 A4 gate passes with backend implementation complete.

## Exact Source References

- `C:\repositories\CanDoItAll\tests\Integration\CanDoItAll.Tests.Integration\AgentFrameworkExecutionRunTrackingIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\Integration\CanDoItAll.Tests.Integration\FileSandboxWorkspaceStoreLockIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\Unit\CanDoItAll.Tests.Unit\MafRuntimeArchitectureServicesTests.cs`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.AgentFramework\Providers`
- `C:\repositories\CanDoItAll\src\Processes\CanDoItAll.Processes.Application`

## UI Composition Contract

- N/A; UI source remains unchanged until this gate passes.

## Deliverables

- Baseline/after cold/warm metrics and operation-count report.
- Targeted .NET performance anti-pattern scan results for touched hot paths.
- EF query review and generated SQL/query-count proof where applicable.
- Backend architecture review and A5 go/no-go record.

## Dependency Impact

- SB06 is forbidden without an honest pass. Cosmetic UI work cannot conceal a backend regression or unproven source-of-truth/concurrency risk.

## Validation Depth

- Proof tier: `Governed`.
- Hard backend-to-UI gate across Core/MAF/Persistence/Infrastructure/Processes.

## Implementation Steps

1. Run targeted anti-pattern scans and manually classify relevant hits.
2. Run unit/integration concurrency, cancellation, lifecycle, file-lock, and EF query-shape proof.
3. Execute the same baseline matrix with the completed backend and record median/p95 plus operation counts.
4. Compare cold/warm/new/existing-session/skills-tools/lock-contention scenarios.
5. Capture CodeAnalytics/dependency snapshot and C# architecture review.
6. Record A5 go/no-go; repair/reopen SB03/SB04 rather than starting UI on failure.

## Scope Exceptions

- Network/provider first-token latency is excluded from distribution proof; one real provider run remains SB07.

## Do Not Do

- Do not assert brittle tight wall-clock CI thresholds.
- Do not optimize unrelated LINQ/string/sealing findings.
- Do not parallelize mutable runtime stages or one DbContext.
- Do not start SB06 before the signed go decision.

## Acceptance Checklist

- [x] Same harness/scenarios used before and after.
- [x] First activity precedes blocked catalog load.
- [x] Duplicate read/write counts decrease as planned.
- [x] Cold and warm medians/p95 are reported separately.
- [x] EF SQL/query counts and no-tracking/projection are reviewed.
- [x] No blocking resource/concurrency/architecture regression remains; three P2 residuals are explicit.
- [x] A5 decision is explicit.

## Proof Required

- `proof/SB05/manifest.md`, before/after report, raw transcripts, scan counts/classification, query proof, architecture snapshot/review, semantic/adversarial invariants, anti-stub audit, and A5 decision.

## Browser Validation Logging

- N/A.

## Progression Gate

- A5 is `Go`: immediate activity, material/justified backend improvement, and all concurrency/query/architecture checks pass.

## Reopen Triggers

- UI/browser evidence showing backend stalls, real mini-model run showing missing phase correlation, or final snapshot revealing cycles/leaks reopens SB05 and owning backend subbundle.

## C# Architecture Contract

- Measure before further optimization.
- Use BCL async/concurrency primitives with explicit ownership.
- EF read parallelism uses separate factory-created contexts.
- Operation counts and semantic correctness outrank micro-optimizations.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Measure and review the completed backend, publish an honest A5 decision, update governed proof, and do not touch UI if the gate fails.
```
