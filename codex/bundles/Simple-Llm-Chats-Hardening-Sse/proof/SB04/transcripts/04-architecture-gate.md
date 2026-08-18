# Architecture review gate

Status: Pass.

- Product owners: `LlmChatExecutionLeaseService`, `LlmChatOperationDispatcher`,
  `LlmChatOperationExecutor`, and `LlmChatOperationTransitions`.
- Persistence adapters: `EfLlmChatOperationRepository` and
  `DatabaseProfileLlmChatExecutionLeaseHeartbeatStore` implement atomic database fences.
- Composition: `LlmChatOperationDispatcherHostedService` only owns loop/scope lifetime and delegates
  all dispatch decisions.
- Old path: production HTTP admission has no provider invocation; the dispatcher executor is the only
  production `conversationEngine.InvokeTurnAsync` call site.
- Testability: fake `TimeProvider` directly exercises lease ownership/expiry; two independent roots and
  real PostgreSQL exercise cross-instance claims/cancellation; real host proves detached request lifetime.
- Partials/references: no production partial expansion and no project-reference change.
- Snapshot: CodeAnalytics `snap-20260815030209-a236038a`, four scoped projects, zero cycles, zero
  diagnostics, no blocking errors. Remaining warnings are existing large-file findings and do not
  identify the new lease/dispatcher owners as a misplaced boundary.

ADR-H05 was implemented as prepared: database-backed competing-consumer lease plus local wake-up,
with fail-closed uncertain dispatch. No architecture deviation was required.
