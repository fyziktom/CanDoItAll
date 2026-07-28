# 02 Typed Agent Activity Stream Foundation

## Status

- `Completed`
- Gate: `Independent Governed A2 Pass`

## Objective

- Introduce the bounded typed sequenced stream and agent operation lifecycle so truthful feedback exists before run creation and cannot be blocked by storage or consumers.

## Success Criteria

- Typed operation/partition/phase contracts compile without string topics; sequence exists only on the SharedKernel envelope.
- First activity publishes before catalog/provider/session work.
- Orchestrator start returns an immediate operation handle with stream ID and completion task; replay begins at sequence zero.
- Per-partition order/fan-out/isolation, bounded retention, terminal replay TTL, global eviction/tombstone gaps, cancellation, completion, cleanup, and reader disposal are proven.
- Slow/throwing compatibility consumers cannot change canonical execution outcome.

## Covered Inputs

- R01-R04 and future SSE projection seam.
- Event-system review, cross-module organization, and immediate preload feedback.

## Prerequisites

- SB01 A1 gate passes.

## Exact Source References

- `C:\repositories\CanDoItAll\src\Foundation\CanDoItAll.SharedKernel`
- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Models\Conversations`
- `C:\repositories\CanDoItAll\src\MAF\Common\CanDoItAll.AgentFramework.Core\Execution`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.AgentFramework\Services\AgentChatExecutionOrchestrator.cs`
- `C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.AgentFramework\Workspace`

## UI Composition Contract

- N/A; backend contract only.

## Deliverables

- Shared typed stream primitive and tests.
- Agent activity records, coordinator/reader, operation lease, singleton DI state, and scoped authorized reader facade.
- Immediate application operation handle/admission contract for send and approval continuation.
- Operation correlation through orchestrator/workspace/runtime.
- Isolated compatibility event notification.

## Dependency Impact

- Preparation, measurements, UI, and future API projections depend on partition identity and lifecycle. Weak ordering/gap/lifetime proof invalidates all downstream consumers.

## Validation Depth

- Proof tier: `Governed`.
- Critical foundation across SharedKernel and Agent Framework Models/Core/Module.

## Implementation Steps

1. Add failing stream/lifecycle/ordering/consumer-isolation tests.
2. Implement singleton-safe typed sequenced primitive and Core coordinator/reader at approved boundaries.
3. Add synchronous start/admission APIs that return stream ID plus completion task before context capture finishes; keep reader cancellation separate from command cancellation.
4. Resolve stable profile/workspace partition, allocate operation ID before context capture, and emit acceptance/preparation phases.
5. Require/persist the initial operation ID on new execution requests, bind the live operation to the run, and project later runtime phases.
6. Make approval end the initial operation as `Suspended`; continuation uses a new operation bound to the same run.
7. Isolate existing multicast notifications and profile subscription lifecycle.
8. Capture producer/consumer/lifecycle matrix, architecture snapshot, and A2 decision.

## Scope Exceptions

- No SSE endpoint, durable event-store replacement, UI rendering, or preparation optimization.

## Do Not Do

- Do not reuse string-based `IActivityStream`.
- Do not expose a global untyped bus.
- Do not await UI callbacks from publishers.
- Do not silently drop retained events.
- Do not put agent/session/context source in the stable partition or assign sequence in agent payloads.
- Do not reopen an operation for approval continuation.

## Acceptance Checklist

- [x] Blocked catalog load still yields first typed activity.
- [x] Concurrent operations are isolated and ordered.
- [x] Retention overrun yields a typed gap.
- [x] Late readers see terminal within TTL; eviction yields tombstone/gap.
- [x] Active partitions are never idle-evicted; all-active capacity returns typed rejection and tombstones are bounded.
- [x] Start returns an operation handle before completion and replay starts at sequence zero.
- [x] Terminal activity occurs exactly once and approval continuation uses a new operation.
- [x] Initial operation/run correlation is stored in a dedicated typed field.
- [x] Every new workspace execution-run entry requires operation identity; only legacy persisted workspace-run records may omit it.
- [x] Throwing/slow readers cannot reverse stored success.
- [x] Partitions and cleanup prevent cross-profile leaks/unbounded growth.

Behavioral acceptance is supported by the exact focused commands in
`proof/SB02/transcripts` and the final independent A2 review.

## Proof Required

- Present: `proof/SB02/manifest.md`, semantic/adversarial invariants,
  producer/consumer/lifecycle matrix, source assertions, scoped architecture snapshot,
  static anti-stub/bypass audit, and exact passing transcripts.
- Verified final-state results: focused unit 58/58; component 65/65; integration 5/5
  plus targeted continuation 3/3; critical downstream unit smoke 403/403; affected Web
  build 0 errors with 125 existing NU1903 warnings.
- Preserved reds: the SB01 `ExecutionUpdated` isolation failure, the six-case A2
  lifecycle-repair failure, and controlled replay/capacity/context/profile mutants.
- Independent closure: `proof/SB02/a2-final-independent-review.md` records `Pass`; all
  source/test/proof hashes and paths were independently verified.

## Browser Validation Logging

- N/A.

## Progression Gate

- The first independent A2 review failed with six findings and remains preserved at
  `proof/SB02/a2-independent-review.md`.
- Remediation evidence and the independent A2 `Pass` are assembled at `proof/SB02`.
- SB03 is authorized.

## Reopen Triggers

- Missing identity for process/project consumers, SSE-incompatible resume semantics, profile leakage, or UI dependence on phase strings reopens SB02 and SB03-SB07.

## C# Architecture Contract

- SharedKernel owns only generic singleton-safe ring/log mechanics and sequence.
- Models own immutable agent values/enums.
- Core owns coordinator/reader behavior and per-operation terminal CAS.
- Module composition owns singleton registration and scoped authorization facade.
- Existing execution-service partials contain only small instrumentation calls; coordinator owns lifecycle logic.
- No optional production dependency that silently disables activity.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Implement the smallest typed operational stream and operation lifecycle, prove all governed invariants, update the execution report, and stop if A2 cannot pass.
```
