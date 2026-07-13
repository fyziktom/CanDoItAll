# 09 Async Operation Workers Inbox Outbox And Timeouts

## Status

- `Completed`

## Objective

- Implement workers for provider polling, operation status updates, event inbox/outbox draining, retention cleanup, cancellation, and timeout transitions.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R04
- R05
- R06

## Prerequisites

- SB06 completed
- SB07 or SB08 available for transport proof

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Operations/CognitiveMemoryScheduledAutomationRunner.cs`
- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Operations/CognitiveMemoryRetentionCleanupService.cs`
- `bundle://architecture/04-runtime-operations-and-feedback.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Implement hosted workers or scheduled background processors for operation polling, provider event inbox/outbox draining, feedback delivery, retention cleanup, cancellation, timeout transitions, and retry/dead-letter handling.
- Use bounded concurrency, lock/lease semantics, idempotency keys, dedupe keys, and observable diagnostics for worker execution.
- Support provider-pushed and host-polled event flows without allowing infinite memory-agent-memory loops.
- Implement operation TTL and feedback retention policies including optional IPFS unpin request emission on forget.
- Add deterministic fake-clock/fake-provider tests for multi-minute operation simulation without slow test execution.

## Dependency Impact

- Long-running providers, eventful memories, and delayed feedback require this before MAF/UI consumers.

## Validation Depth

- `Critical runtime foundation`

## Implementation Steps

1. Add worker queue queries using short-lived DbContext instances and cancellation-aware async loops.
2. Implement state transitions from accepted/running to completed/failed/timed-out/cancelled/expired/dead-letter.
3. Add inbox/outbox dedupe and loop-guard logic before provider events can trigger workflow/agent dispatch.
4. Add metrics/logging points for queue depth, due operations, processed count, failed count, timeout count, and feedback deliveries.
5. Add tests with fake providers and fake time for polling, callback completion, cancellation, retention expiry, and event dedupe.

## Scope Exceptions

- No known scope exceptions for this subbundle at preparation time.
- If implementation discovers an exception, document it in `reviews/01-execution-report.md` and stop before downstream work if the exception affects a phase gate.

## Do Not Do

- Do not implement downstream subbundles early.
- Do not introduce direct generic-memory or MAF references to native Cognitive Memory implementation types.
- Do not add Qdrant as a base runtime dependency.
- Do not expose host EF entities or DbContext instances to memory providers.
- Do not duplicate memory operation dispatch logic outside the shared handler.

## Acceptance Checklist

- The implemented surface is observable through focused tests or explicit proof artifacts.
- Dependency boundaries from `requirements/03-non-negotiable-boundaries.md` remain intact.
- No downstream subbundle work is silently implemented or assumed.
- Execution report is updated with proof paths, command transcripts, and gate result.
- A provider operation that takes minutes is represented as a non-blocking accepted/status flow, not as a blocked HTTP or agent call.
- Provider events are processed through policy and loop guard before any agent/workflow launch request is emitted.
- Feedback delivery and expiration are idempotent and observable.
- Semantic proof would fail against a stub, renamed old implementation, in-memory-only shortcut, or test-only manually seeded signal.

## Proof Required

- Create `proof/SB09/manifest.md` with changed-file hashes, failing-first transcript, passing transcript, source assertions, and anti-stub audit output.
- Create `proof/SB09/semantic-invariants.md` covering raw-note closure, shipped behavior, shallow-pass trap, adversarial negative proof, semantic positive proof, and downstream dependency check.
- Add a `Production Behavior Artifact Matrix` in `proof/SB09/manifest.md` and `proof/SB09/semantic-invariants.md` for every new state, event, ledger record, worker signal, or provider-visible behavior introduced here.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run worker tests using fake time for accepted-to-completed, accepted-to-timeout, cancellation, feedback retention, event dedupe, and dead-letter behavior.
- Capture logs or test assertions proving workers use bounded batches and cancellation tokens.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB09 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB09 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
