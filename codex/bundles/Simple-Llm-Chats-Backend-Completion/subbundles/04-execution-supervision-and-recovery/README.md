# SB04 — Execution Supervision And Recovery

## Status

- `Ready`

## Objective

- Give the executor deterministic ownership of provider task lifetime and expose safe evidence-driven recovery for operations that outlive a claim or host.

## Success Criteria

- Heartbeat/lease/profile/shutdown exits cancel when required and always drain/observe provider work before scope/registration disposal.
- No unobserved exception, false success, duplicate dispatch, or provider task remains after executor completion.
- Manage-scoped reconcile route settles evidence-proven outcomes, rejects live owners, and leaves ambiguous post-dispatch operations recovery-required without redispatch.
- CP1 lifecycle architecture review passes.

## Covered Inputs

- BC-030 through BC-033.

## Prerequisites

- SB03 `Pass` with canonical transaction invariants current.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationExecutor.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationCancellationRegistry.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationStateMachine.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatExecutionLeaseService.cs`
- `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatOperationApplicationService.cs`
- `repo://src/App/CanDoItAll.Web/Api/LlmChatOperationsApi.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/LlmChatOperationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/LlmChatsTurnApiIntegrationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/LlmChatsApiPostgreSqlIntegrationTests.cs`

## UI Composition Contract

- N/A — executor/recovery HTTP only.

## Deliverables

- Structured provider-task supervision on all executor paths.
- Explicit typed classification for control failure before/after dispatch evidence.
- `POST /api/llm-chat-operations/{operationId}/reconcile` contract, metadata, policy, mapping, docs.
- Reducer/reconcile behavior for known failed/cancelled/succeeded evidence and ambiguous dispatch.

## Dependency Impact

- Critical foundation: audit/SSE/retention cannot be trusted until provider ownership and recovery are deterministic.

## Validation Depth

- Proof tier: `Behavioral`.
- Test solutions: Unit and Integration lanes.
- Filters: exact new executor/recovery cases plus `LlmChatsRecoveryApiIntegrationTests`.
- Selection reason: direct task lifetime needs deterministic unit barriers; route/status/durable evidence needs real host/PostgreSQL.
- Expected named cases: `Heartbeat_failure_cancels_and_drains_started_provider`, `Provider_failure_during_heartbeat_failure_is_observed_once`, `Shutdown_cancels_and_drains_provider_before_scope_release`, `Profile_switch_after_dispatch_preserves_recovery_required_without_redispatch`, `Reconcile_known_failed_attempt_settles_failed`, `Reconcile_known_cancelled_attempt_settles_cancelled`, `Reconcile_committed_transcript_settles_succeeded`, `Reconcile_ambiguous_dispatch_remains_recovery_required`, `Reconcile_rejects_live_owner`, and `Reconcile_route_requires_manage_scope_and_returns_stable_errors` (10 cases).
- Invalidation keys: executor, heartbeat/lease, provider port, cancellation registry, state machine/reducer, operation service, reconcile route/auth.
- Broad-gate decision: deferred to SB10 for hosted execution and public route changes.

## Implementation Steps

1. Add provider-start barrier tests where heartbeat/control/shutdown fails after task creation.
2. Refactor executor into explicit task ownership: linked cancellation, one outcome classifier, and guaranteed drain/observation before cleanup.
3. Preserve cancellation semantics and distinguish pre-dispatch safe failure from post-dispatch ambiguity.
4. Extend reducer/reconcile only for durable evidence-proven transitions; prove no provider port call from reconcile.
5. Add manage-scoped Web route, stable validation/metadata, and documentation.
6. Build Core/Persistence/Web/Composition as affected; list/run exact Unit and PostgreSQL host cases.
7. Obtain independent CP1 lifecycle ownership review.

## C# Architecture Impact

- Executor control-flow and recovery authority change inside existing application/Web owners.

## Boundary Ownership

- Executor owns task lifetime; reducer owns deterministic state decision; Web exposes command only; Persistence remains evidence authority.

## Dependency Direction

- No provider runtime dependency is added to Web and no recovery logic moves into the hosted service.

## Pattern Decision

- PSR-03 and PSR-04.

## Testability Contract

- Barrier-controlled tasks and tokens; completion means the provider task is completed/observed and registry/scope released in that order.

## Partial Class Policy

- No partials; extract a private/local supervision helper only if it shortens ownership without adding an interface.

## Architecture Proof Required

- CP1 review of dispatch ownership, cleanup order, reducer evidence, and no-redispatch source/behavior proof.

## Scope Exceptions

- Worker concurrency/queue duration is SB07; this unit fixes one execution's ownership.

## Do Not Do

- Do not fire-and-forget provider work, suppress its exception, or redispatch ambiguous recovery.
- Do not make reconcile read live process-local registry state as canonical.

## Acceptance Checklist

- [ ] Ten named cases discover and pass.
- [ ] Provider task is completed/observed on every tested exit.
- [ ] Reconcile route and exact scope pass real-host proof.
- [ ] CP1 review passes.

## Proof Required

- Failing-first/passing task timelines, exact discovery, token/task/registry assertions, PostgreSQL evidence snapshots, route response samples, builds, and CP1 decision under `proof/SB04`.

## Browser Validation Logging

- N/A — no rendered UI.

## Progression Gate

- SB05 may start only after CP1 is `Pass` with no orphan/false-success/unsafe-recovery finding.

## Reopen Triggers

- Any later executor, provider port, lease/heartbeat, cancellation, reducer, operation status, or reconcile route change reopens SB04 and SB05-SB10.

## Suggested Agent Prompt

```text
Execute SB04 only. Prove provider task ownership on every exit and add evidence-only reconciliation. Stop rather than redispatch an ambiguous post-dispatch operation.
```
