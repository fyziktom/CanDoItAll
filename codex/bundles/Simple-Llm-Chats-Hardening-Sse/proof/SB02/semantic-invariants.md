# SB02 semantic invariants

| Invariant | Evidence | Result |
|---|---|---|
| Admission is atomic. | `LlmChatOperationAdmissionService` locks the conversation and commits operation claim, user entry, active-turn state, and admission evidence through one scoped unit of work; PostgreSQL failure injection rolls all of it back. | Pass |
| Provider I/O is outside a database transaction. | The facade completes admission before calling `ILlmChatConversationEngine.InvokeTurnAsync`; finalization starts a new transaction. | Pass |
| Success finalization is atomic. | `LlmChatOperationStateMachine.CommitSuccessAsync` locks the operation, rechecks cancellation, and commits assistant, usage/evidence, active-turn clearing, and Succeeded together; PostgreSQL failure injection rolls all of it back. | Pass |
| Failure/cancellation compensation is exact and atomic. | The state machine compensates by exact conversation/operation identity and commits transcript clearing with terminal operation state; rollback proof preserves both as nonterminal. | Pass |
| Compensation exhaustion cannot masquerade as terminal. | Failed compensation advances the durable operation to `RecoveryRequired`; a unit test proves the active turn is not hidden behind Failed/Cancelled. | Pass |
| Committed cancellation wins finalization ordering. | `CancellationGeneration` is monotonic and finalization re-reads the locked operation. `CompleteTranscript` excludes `CancellationRequested`. | Pass |
| Replay precedes mutable lifecycle validation. | Admission resolves operation identity/fingerprint before current definition/conversation eligibility; replay after archive returns the prior result without dispatch. | Pass |
| Possible prior dispatch is never automatically repeated. | The pure reducer maps started/ambiguous durable dispatch evidence to `RequireRecovery`. | Pass |
| Archive cannot race active work. | Archive locks the conversation row in the same transaction and rejects active-turn or nonterminal-operation state. | Pass |
| Direct and restart decisions share one reducer. | `LlmChatOperationReducer` consumes durable operation, transcript, and invocation evidence for live completion and reconciliation. | Pass |
| Provider attempt outcome is deterministic. | Deadline audit is persisted as Failed/DeadlineExceeded, matching direct and restarted reduction. | Pass |
| Stream-event integration remains correctly owned. | SB02 commits operation lifecycle evidence atomically. The additive durable stream-event journal and atomic lifecycle-event rows remain explicitly owned by SB08/RQ-022. | Pass for SB02 boundary; extended by SB08 |
