# Current State Review

## Source-backed findings

The previous implementation moved the branch from a restored deterministic process runtime into a read-only verification-host beta:

- `ProcessVerificationRuntimeHost` now exposes `VerifyAsync(ProcessVerificationHostRequest, CancellationToken)` and returns `ProcessVerificationHostResult` with either `Response` or structured `Denial`.
- Host policy options exist: global enable/disable, per-lane enablement, max payload items per lane, and max supplied evidence content bytes.
- Lane registry/selector remains explicit and non-reflective; no fallback selector is allowed.
- `EfCoreProcessVerificationAuditStore` and `ProcessVerificationAuditEntry` exist, and `AddProcessesModule` now calls `AddEfCoreProcessVerificationAuditStore()` after the core host registration.
- Manager read-only verification facade/readback contracts exist.
- The live process-run OpenAI smoke now proves a real `ProcessRun` path: `ProcessesService.StartRunAsync`, `ResolveAssignmentAsync`, `IProcessRunAutomationDispatchService.DispatchAsync`, AgentFramework execution-run readback, and provider usage observations.

## Real test outcome

The latest proof reports:

- solution build: 0 warnings / 0 errors;
- full unit: 1134 passed, 0 failed, 0 skipped;
- focused verification-host/live smoke: 46 passed;
- live process-run OpenAI smoke: passed, not skipped, API key not printed.

## Critical remaining gaps

1. Durable audit exists, but it needs stronger production/runtime proof: EF model inclusion, migration/bootstrap, query after new service scope, query after app restart/profile reload, redaction retention, and bounded query behavior.
2. The host still has a sync compatibility `Verify(...)` wrapper. Runtime, manager, API, scheduler, and workflow paths must use `VerifyAsync(...)` only.
3. The current live smoke uses bounded execution but default token budget remains too permissive in test code. Future live proof should require explicit budget/model/timeout or use a much smaller default.
4. Manager readback currently has service/API proof; it still needs operator-visible UI integration on the process run detail surface.
5. Scheduler/workflow verification job readiness is modeled, but it should be executed end-to-end as read-only verification jobs without direct driver hooks.
6. The next move toward a generic runtime host should be governance/sandbox readiness, not execution-capable drivers yet.