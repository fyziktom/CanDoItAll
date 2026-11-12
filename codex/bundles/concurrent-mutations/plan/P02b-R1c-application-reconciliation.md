# R1c: application receipt reconciliation and honest continuation results

Status: implementation source authored in the managed checkout; **not built or executed**.
Parent released this source slice after R1a proof while R1b validation is coordinated
separately. See [the source checkpoint](../validation/P02b-R1c-application-source.md)
for exact authored cases, source ownership and the remaining validation gates.
R1c remains dormant: existing producer factories and the MAF unsupported-effect-only
guards stay unchanged until R1d/R2. This does not activate Process, detached, workflow
or chat producers, nor the separate P04/P07 freshness policy settings.
[The bounded reconciliation amendment](P02b-R1bc-bounded-reconciliation.md) controls
slice budgets, cursor compatibility, pinned immutable lease data, the single conditional
checkpoint and compact response. It supersedes an all-proposal loop or a separate
application summary write.

## Decisions and buildable order

1. Add the public, sanitized effect-continuation carrier and constructor invariants;
   make existing mapper/chat/Process consumers compile with its explicit absence of
   provider output. Preserve all normal-execution results and persisted usage.
2. Add one cohesive Core application helper and one early branch in the existing
   execution service. Connect R1b receipt lookup, verified current-read resolution and
   conditional waiting-state persistence. Preserve schema-5 journals at startup.
3. Project the same truth through existing REST/SSE/native surfaces and activity.
   Complete current-read, persistence-failure and no-provider proofs. No new endpoint,
   execution-service partial, generic recovery coordinator, owner registry or SDK
   checkpoint representation is required.

The source slice is ready only when all three parts compile together; a nullable
property added without its actual consumers is not an acceptable checkpoint.

## Existing entry points and exact branch

Keep `RecoverExecutionRunAsync` and `ContinueExecutionRunAsync` signatures, existing
workspace/current-profile facades and `/api/agents/execution-runs/{id}/recover` and
`pending-approvals` routes. `AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
already funnels recovery into `ContinueExecutionRunEntryAsync(...,
recoverAdmittedTools: true)`. After loading the original run and
`EnsureActivityRunBinding`, branch on a validated schema-5 EffectJournalOnly journal,
before the completed-result metric loader, `RequireRecoverableToolJournal`, attachment
restoration, SDK/checkpoint compatibility, provider preparation or write-authority
resume validation. Cancelled runs retain the explicit cancelled-reconciliation route.

Use one new `Execution/AgentExecutionEffectReconciliation.cs` application class with
the following narrow entry point, called by that existing method:

```csharp
Task<ExecutionRunResult> ReconcileAsync(
    ExecutionRunRecord original,
    IAgentExecutionActivityOperationLease activity,
    CancellationToken cancellationToken);
```

Its dependencies are the existing journal, run report/store and atomic run writer,
the existing execution-authority resolver, current workspace profile identity and
installed `IAgentToolReceiptReconciliationProvider` instances. Reuse composition in
`AgentFrameworkWorkspaceService`/the workspace factory; do not give it a provider,
SDK runtime, attachment restorer, tool invoker or service locator. It acquires R1b's
interrupted read-only lease, binds it and reconciles using its admitted immutable
snapshot and the detail returned from the final conditional checkpoint.
Do not hold a second dispatch lease around an acquisition that already owns one.

R1b's one final atomic checkpoint includes WaitingOnTool/null terminal outcome and
the actual continuation requirement together with confirmations and cursor. R1c adds
no separate reload/summary save. Compare session, profile/generation, run/chat identity,
active lease ID, original run/journal revision, state and exact approvals. Preserve
metrics, usage, messages, artifacts, original Result/DisclosureEvidence and every intent.
A concurrent Cancelled/Completed state, decision, replaced lease or changed journal
cannot be overwritten with a stale waiting snapshot. Owner lookups and current-read
resolution occur outside the file mutation callback. Failed comparison returns a
typed conflict with no status promotion; already observed commitment is nonpersisted
knowledge only under currently allowed run read. No completion notification is published.

Approval dispatch has an explicit capability gate in the existing composition/factory,
checked **before** applying decisions. R1c cannot enter MAF or invoke an approved saved
tool. Do not register an implementation that silently falls back to ordinary provider
continuation. Test-only schema-5 fixtures may exercise receipt recovery. R1d will supply
the exact saved-proposal dispatcher and its proof before the gate can be enabled.

An approved self-contained Prepared proposal is preserved as a receipt no-op. It does
not acquire ProviderInputRequired or EffectReconciliationRequired merely because R1d
is unavailable. Pending approval is separate as well. Receipt-only recovery may return
its waiting snapshot with requirement None and an explicit `HasPreparedEffects` flag
derived from the saved journal; that is not an offer to dispatch or a completed task.
Approval requests remain explicitly unsupported under this dormant capability gate.

## Trusted current read, separate from effect authority

R1b supplies `Func<AgentToolSessionObservation, CancellationToken, ValueTask<bool>>`.
The journal constructs the observation from its verified lease/session, saved turn
reference and governance. The application must not deserialize a client actor or call
the current dispatch-only `RequireSessionObservationAsync` on a read-only lease.

For an interactive session, validate the original authority/session ID, original read
allowance, agent, profile/generation, workspace scope/schema and captured project
lifetime. Resolve current permission with
`IAgentExecutionAuthorityResolver.ResolveAsync(AgentExecutionAuthorityResolutionRequest.FromCaptured(...))`
using that original source and governance. Require the same identity/material scope
and current ReadAllowed. The canonical resolver assigns a fresh observation authority
ID: do not replace the original journal identity with it or require its generated ID
to equal the old one. Do not require today's write permission or complete dispatch
policy fingerprint to disclose a receipt. `ResolveAdmittedRuntimeContext` and transient
attachment restoration are inappropriate here: current read must still work when the
original input is gone. The existing Workbench result-source observation is a useful
behavioral example, not a new Core dependency on Workbench.

For a governed background session, retain the exact saved SourceKind/SourceId,
fingerprint, purpose and profile binding; use the existing registered owner policy's
ReadAllowed independently of DispatchAllowed. Journal/owner checks run before and
after lookup as R1b specifies. An expired Process claim may deny a new effect while
allowing current read. Missing/changed source identity or policy fails closed. Detached
sources remain gated on R2's typed observation; there is no SourceKind string fallback,
LocalWorkspace caller substitution or manufactured interactive governance.

Projects' receipt provider already checks its claim before/after lookup and the current
target read policy. It supplies no mutation lease for receipt-only lookup. A successful
run-source read does not override target permission. The old cancelled path's single
`bool currentReadAllowed` is not evidence of a new final-current guarantee; preserve
its existing semantics until the callback-overload migration is explicitly released.

## Owner observations and retained commitment

`Workbench/AgentTools/ProjectDefinitionToolReceiptRecovery.ReconcileAsync` (the approved
read-owner extraction from `ProjectDefinitionToolAdmission`) maps all
lookup outcomes other than Found/NotObserved/ClosedAbsent to access denial. Correct
only that classification when R1c is implemented:

| Owner lookup | Receipt provider / application meaning |
| --- | --- |
| FoundFull / FoundScalars | Exact original receipt, under current target read, then final claim/current-run read. |
| NotObserved / ClosedAbsent / Expired / DetailUnavailable(TargetUnavailable) | Conservative `NotObserved` with no payload. Expiry is not a claim that the effect was rejected or the horizon remains open. |
| DetailUnavailable(AccessDenied) | Explicit current access denial; disclose no cached payload. |
| UnsupportedVersion / InvalidStoredResult | Explicit unavailable/corrupt evidence, not absence or access denial. |
| DatasetChanged / CallerChanged / InvalidContext | Explicit original binding/context refusal; never refresh namespace, epoch or caller. |

Projects returns expiry/target-unavailable without historical payload; the current
saved-run read gate still applies before returning any run-bound status. The generic
provider does not need a new owner-specific absence taxonomy for this read-only step.
Ordinary bounded recovery does not requery OwnerReceiptConfirmed; it reports
KnownCommitted with detail not requested. For R1d's explicit detail restoration, only
an already confirmed proposal can map a later miss/expiry/missing reader to
KnownCommittedDetailUnavailable. An unconfirmed intent remains uncertain.
Contradictory decoded evidence fails explicitly; it cannot replace the saved receipt.

The public projection separates knowledge, detail availability and access. On current
denial, return the typed access error with no receipt/source payload. Existing locally
displayed commitment must not be downgraded to uncertainty or rejection; do not reveal
a formerly unseen historical commitment to a caller lacking current run read. Where
run read is allowed but target detail is denied, the saved run's already-confirmed
commitment may remain visible as knowledge with denied detail, never as authorized
receipt contents. Every UI retains its exact operation identity and offers no effect
retry from access denial, lookup failure or unavailable detail.

R1b's actual 24 MiB aggregate journal and 8 MiB per-envelope bounds remain real.
Confirmation followed by capacity/write/ack
failure preserves the exact original intent; it does not create a replacement operation,
SDK result or a successful persistence claim. The bounded typed error retains transient
commitment knowledge when this call actually observed it, without returning raw owner
bytes. Later recovery uses the same saved operation. A crash can lose unpersisted
knowledge, and an expired owner horizon can prevent later recovery; this card does not
promise to solve exhausted journal capacity by eviction or an unbounded limit.

## Result contracts: no fabricated provider output

Add a Models-owned `AgentEffectContinuation` record with original run/chat identity,
`AgentToolContinuationRequirement`, `HasPreparedEffects`, checked aggregate snapshot
counts, slice progress/stop/persistence status and at most four sanitized observations
(exact stored approval binding and closed knowledge/detail status). Enforce the
amendment's 16 KiB serialized bound; omit unbounded ToolName, reject duplicate/foreign
bindings, and distinguish unresolved from unexamined work. The journal maximum is
not a public list budget. The public shape carries no prepared payload,
SourcePreparation, owner receipt bytes, source ID, SDK Result or DisclosureEvidence.
The requirement and each proposal's persisted commitment are independent: both
`KnownCommittedDetailUnavailable` and an uncertain effect can require reconciliation,
but must render differently. `ProviderInputRequired` never authorizes synthesis of a
missing original tool output. A supplied new input is a later semantic decision.

Keep normal public constructors for `ExecutionRunResult` and `AgentChatRunResult`
requiring nonnull metrics (and a nonnull assistant message for normal chat). Replace
positional permissiveness only as far as needed with explicit validating constructors
and immutable metric/discriminator properties. Add `ForEffectContinuation(...)`
factories for the typed mode. A nullable metric by itself, or public `with`/init access
that can manufacture a normal null-metric result, is insufficient enforcement.

For a receipt-only result, Metric is null, ResponseText is empty, AssistantMessage and
StructuredOutput are null, State is WaitingOnTool and ContextCompletionNotification
is null. The typed continuation is required. Keep all existing persisted metrics and
usage unchanged in run detail; do not return the last provider metric as newly incurred
recovery usage. Activity timestamps cover recovery duration. Normal results retain
their current wire shape and semantics; only the new typed effect branch permits the
nulls. GET/run-detail projection must reconstruct the sanitized continuation from the
validated journal, not expose ToolAdmission or depend solely on the latest command.

The current `GetExecutionRunDetailAsync` only loads/enriches detail; the mapper alone
cannot provide that new disclosure guarantee. Add a narrow application helper
`ReadContinuationAsync(ExecutionRunRecord original, CancellationToken cancellationToken)`
to validate the saved session/run/profile/governance and resolve the same current
source-read policy before attaching the summary. It performs no owner lookup,
dispatch-lease acquisition or journal mutation. Share the original-identity validation
with the recovery adapter rather than pretending this read has an invocation claim.
Current run-read denial omits/refuses the new summary rather than exposing bindings.
This change does not redefine unrelated legacy GET fields' authorization.

For this readback, persisted commitment is `KnownCommitted` with detail not requested;
do not imply that current owner detail was fetched or found. The closed public status
set is `ReceiptConfirmed`, `KnownCommitted`, `KnownCommittedDetailUnavailable`,
`KnownCommittedDetailDenied`, `ReceiptNotObserved`, and `ReaderUnavailable`. The first
requires this call's authorized receipt observation; the three other committed variants
require original persisted confirmation (or explicit transient confirmation in a
persistence-failure response). `KnownCommittedDetailDenied` is allowed only when current
run read succeeded but target detail read was denied. None carries receipt contents.
No summary is returned on denied run read; local clients can preserve already-known
commitment without receiving additional historical information.

Known-committed/detail-unavailable, uncertain, access-denied and prepared-but-undispatched
presentations must stay distinct. None of them adds an assistant transcript entry,
completion notification, successful Process outcome, synthetic finalizer output or new
provider usage observation. The parent approved this narrow metric contract change;
the earlier proposed zero-token provider metric is superseded.

## Concrete consumer and source inventory

| Owner / existing file or method | Required change |
| --- | --- |
| Models `Conversations/ConversationModels.cs`, `Agents/AgentModels.cs`; new `Execution/AgentEffectContinuation.cs` | Validating result factories and sanitized carrier; existing RunOutcome/ExecutionState numbers unchanged. |
| Core `Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` | Early typed branch and waiting activity terminalization; no new execution-service partial. |
| Core `Execution/AgentFrameworkWorkspaceExecutionService.ContinuationGuards.cs` | Do not route effect-only waiting state through metric-required `LoadExistingExecutionRunResultAsync` or attachment/write checks. Keep old schema guards. |
| Core `Execution/AgentFrameworkWorkspaceExecutionService.Chat.cs` | `ToAgentChatRunResult` preserves the effect carrier without requiring or fabricating an assistant message. Normal SendMessage still requires actual output. |
| Core new `Execution/AgentExecutionEffectReconciliation.cs`, existing workspace factory | Only the dependencies and read-only entries described above; explicit disabled saved-effect dispatch capability. Existing public workspace/current-profile facade signatures remain. |
| AgentFramework `Execution/AgentFrameworkExecutionRecoveryService.cs` | Preserve valid schema-5 EffectJournalOnly at startup, including interruption before its first segment. Use existing cross-instance lease and atomic writer; do not clear source/intents or turn it into legacy cancellation. No receipt lookup or SDK invocation during startup. |
| Workbench `AgentTools/ProjectDefinitionToolReceiptRecovery.cs`, `ProjectDefinitionGovernedReceiptAccess.cs`, existing Process authority reader/service | Narrow lookup classification and pinned-claim original-evidence read path from the amendment; no new effect path or changed caller binding. Ordinary GUID reads and all mutation admissions remain. |
| Web `Api/AgentApiResponseContracts.cs`, `AgentsApi.cs` | Conditional metric/chat message mapping, typed continuation and sanitized GET projection; existing route/auth and typed access errors retained. No raw owner/journal payload. |
| Web `Api/AgentEventsApi.cs` | Same continuation in `agent.command.completed`; that means the HTTP command ended, not the agent task succeeded. Emit approval-required only for actual pending approvals. Typed admission/access failure remains distinct from completed waiting; disconnect cannot undo a confirmed effect. |
| AgentFramework `Services/AgentChatExecutionOrchestrator.cs` | Do not publish task completion for a receipt-only waiting result; retain activity ownership/profile checks. |
| AgentFramework `Pages/Components/AgentChatPanel.razor.cs` | Recovery readback distinguishes known commitment, uncertainty, input and pending approval. Preserve draft/thread/profile generation fences; no fake new-send readiness. Voice reads only an actual assistant message. |
| UI `Chat/AgentChatActionState.cs`, `AgentChatRecoverySurface.razor`; existing activity status renderer | Typed status supplied by host; no reads/writes in renderer or automatic recover/retry. |
| AgentFramework `Pages/Components/ScenarioHarnessPanel.razor.cs` | Render typed waiting without saying approval/completion when neither happened. |
| Processes `Services/RuntimeIntegration/ProcessAgentExecutionRecoveryPolicy.cs`, `AgentFrameworkProcessStepExecutor.cs` | Branch on the typed continuation before metric outcome/transient-retry/output parsing. Return existing needs-manager/manual handling with original execution ID and a closed diagnostic code; never classify it as retryable pre-effect failure. No Process producer activation in R1c. |
| Processes `Services/RuntimeIntegration/AgentFrameworkProcessRunNarrativeGenerator.cs`, `Components/ProcessWorkspaceShell.razor` | Narrative requires actual completed provider output; voice requires an actual message. Do not parse empty recovery output or invent a model. |
| Web `DevelopmentDiagnosticsEndpoints.cs` | Explicit typed waiting/no-provider result; no nullable metric dereference or zero-cost/success fabrication. |

These are actual direct/indirect consumers found in source. Further constructor call
sites must be rechecked during implementation; this is not permission to weaken normal
constructor invariants to avoid updating a caller. Models stays inward of Core;
Modules compose real policies; UI receives sanitized Models/contracts. Core never
references Workbench, Projects, Web, Process implementation or UI.

## Activity and serialization compatibility

The existing activity union cannot accurately represent this state: coordinator
`Suspend` and Models `ValidateTerminalState` force Suspended to AwaitingApproval.
Append `AgentExecutionActivityPhase.AwaitingContinuation = 17`; values 0 through 16
and existing `AgentExecutionActivityTerminalOutcome.Suspended = 3` remain unchanged.
Add a typed `SuspendForContinuation(AgentEffectContinuation continuation, string message)`
on the existing activity operation, or the equivalent narrow validated overload.
It supports the explicit prepared-only continuation with requirement None; do not
require a fabricated non-None reason. Retain ordinary `Suspend` for real approvals.

Update `AgentExecutionActivityTransitionRules`, coordinator and Models validation
together: Suspended accepts precisely AwaitingApproval or AwaitingContinuation, while
the typed operation selects the correct one. Invalid terminal/phase combinations
still fail. The public continuation result carries detailed requirement/knowledge;
activity needs only the truthful phase, not raw receipt or duplicated proposal state.
REST retains numeric enum encoding and SSE retains its configured camel-case strings;
the added SSE value is `awaitingContinuation`. Persisted old activities/results and
schemas 1–4 retain their old meaning/default omission. Any generated enum client update
belongs to the same contract change before live activation.

## Cancellation, startup and failure boundaries

R1c does not use interruption recovery to revive RunOutcome.Cancelled. The existing
cancel endpoint retains exact pending-approval cancellation and no dispatch; its
receipt route remains separate. Do not broaden `AgentPendingApprovalCancellation`
to Process/background sources merely to share a helper. A schema-5 cancellation
adapter requires its own callback/state proof before activation.

Startup preservation validates the whole journal before using its support flag,
retains original pending approvals/results/usage and respects existing run leases.
Zero-segment EffectJournalOnly is meaningful before a first provider response. Malformed
journals fail explicitly rather than being cleared or silently treated as legacy.
Startup may retain its existing failed/interrupted historical marker; explicit
reconciliation later persists the truthful waiting state. It never marks a Process
step successful or recreates authority/attachments.

Conditional checkpoint failure, receipt read outage and access revocation
must not be collapsed into a known rejected effect. A late known acceptance wins over
an earlier uncertain UI view. Existing persisted confirmation remains on disk after
all later failures; transient commitment from a failed confirmation write is reported
as such only under allowed disclosure. No error handler creates a new epoch, intent,
target, approval, reservation or provider call. Logs identify run/binding and the failed
phase, without saved requests, receipt bodies or credentials.

## Authored source inventory: 28 new rows, zero discovered or executed for R1c

Use existing file-store, activity, API and component fixtures with a test-only validated
schema-5 seed. No live producer factory is changed for a test. Two owner-policy cases
reuse the existing owned Projects/Process fixture if actual SQL authority is needed;
do not create a new stack. The following source cases are Facts, one row each; there are no
new InlineData expansions. Re-derive actual attributes and exact filters before parent
discovery. Existing regression selections are separate and not included in this 28.

| New test class / assembly | Exact prospective test names | Rows |
| --- | --- | ---: |
| `AgentEffectContinuationResultTests` / Unit | `Normal_results_reject_missing_metric_or_chat_message`; `Effect_factory_requires_typed_waiting_and_never_provider_output`; `Legacy_result_and_activity_serialization_remain_compatible`; `Awaiting_continuation_phase_roundtrips_and_rejects_invalid_terminal_pairs` | 4 |
| `AgentEffectRecoveryApplicationTests` plus one Fact in `ProcessCatalogAuthorityPersistenceTests.EffectApplication.cs` / Integration | `Recovery_precedes_attachment_provider_and_checkpoint_access`; `Interactive_write_revocation_allows_current_read_without_replacing_original_authority`; `Current_read_revocation_before_or_during_lookup_blocks_disclosure`; `Changed_agent_profile_or_generation_cannot_rebind_original_intent`; `Expired_process_dispatch_can_read_original_receipt_through_application_without_new_effect`; `Confirmed_summary_stays_known_committed_without_querying_expired_detail`; `Checkpoint_failure_preserves_exact_intent_and_reports_no_durable_waiting_promotion`; `Prepared_receipt_noop_and_unsupported_approval_dispatch_preserve_decisions`; `Cancelled_runs_remain_on_separate_no_dispatch_reconciliation`; `Startup_preserves_valid_effect_journal_including_zero_segments_without_sdk_work` | 10 |
| `AgentEffectRecoveryApiTests` / Integration | `Rest_recovery_returns_typed_waiting_without_metric_or_assistant`; `Run_detail_reconstructs_sanitized_continuation_without_journal_payload`; `Rest_recovery_and_existing_activity_stream_preserve_truthful_waiting`; `Denied_or_unavailable_receipt_response_cannot_disclose_cached_bytes_or_offer_retry`; `Disconnected_recovery_does_not_erase_confirmed_effect_or_publish_task_completion` | 5 |
| `AgentEffectContinuationConsumerTests` / Unit | `Chat_mapper_preserves_the_original_effect_checkpoint_without_fabricating_a_message_or_usage`; `Process_recovery_retains_the_original_run_and_refuses_a_transient_replacement_dispatch`; `Narrative_generation_defers_the_same_source_instead_of_parsing_saved_effects_as_provider_output`; `Recovery_orchestrator_suspends_original_activity_without_publishing_context_completion`; `Prepared_only_readback_keeps_the_original_intent_and_does_not_request_provider_input_or_dispatch` | 5 |
| `AgentChatPanelResponsivenessTests` (new `AgentEffectContinuationSurfaceTests.cs` partial) / Components | `Native_recovery_distinguishes_known_commitment_from_uncertainty_and_access_denial`; `Native_recovery_keeps_original_draft_and_profile_generation_fence`; `Voice_and_scenario_surfaces_require_actual_assistant_output`; `Awaiting_continuation_never_renders_as_approval_or_success` | 4 |

Exact new selections are recorded in [the source proof](../validation/P02b-R1c-application-source.md):
four full new classes, one exact Process method and the four exact new native methods.
Do not select the full existing partial Process or native class and call its rows new.
Reuse existing `AgentAdmittedRunRecoveryIntegrationTests`,
`AgentRecoveryApiIntegrationTests`, `AgentToolCancelledReadRecoveryIntegrationTests`,
`AgentFrameworkExecutionRecoveryIntegrationTests`, `AgentApprovalCheckpointRecoveryTests`,
`AgentExecutionActivityCoordinatorTests`, `ProcessAgentExecutionOutcomeRecoveryPolicyTests`,
`AgentChatPanelResponsivenessTests.Recovery`, `AgentPendingApprovalCancellationUiTests`
and `AgentExecutionActivityStatusTests` as affected regression sources; source-count
their actual selected methods separately. Their historical counts are not new proof.

Exit requires source review, serialized standard builds, exact discovery/execution
counts and parent-owned static gate. R1d/R2, actual producer cutover, nested freshness
policy activation, cancellation callback migration and legacy nonjournal paths remain
explicitly open after this card. No R1c result can claim end-to-end runtime protection
until those required producer and dispatcher gates close.

## Implemented transport and cleanup clarification

There is no recovery-command SSE POST in the existing API. The routed proof uses
`POST /api/agents/execution-runs/{id}/recover`, then the existing
`GET /api/agents/execution-operations/{operationId}/events/stream`. The latter emits
AwaitingContinuation/Suspended without approval or task-completed events. Existing
streamed commands can carry the same typed continuation but are not described as a
new recovery endpoint.

A confirmed checkpoint followed by coordination-file disposal failure now throws
`AgentEffectReconciliationCleanupException`, retaining the already authorized confirmed
continuation and the original cleanup exception separately. The lease only closes its
FileStream; disposal neither clears the persisted active lease ID nor changes receipts.
Activity remains waiting with a needs-attention message. HTTP returns 503 with
`agents.effect-lease-release-unconfirmed` and confirmed bounded knowledge. Process
requires manager review and forbids a replacement retry. Native rendering retains a
sticky release-unconfirmed flag and blocks another slice; SnapshotRead alone cannot
clear it. There is no invented cleanup command. An operator must inspect/release the
owning process or coordination resource and retain the original run; the later native
restart/admission seam remains a gate. An incomplete checkpoint plus cleanup error
keeps the exact typed continuation and sets an immutable release-unconfirmed flag;
both diagnostics remain in its inner aggregate. If no continuation was authorized,
`AgentEffectReconciliationLeaseException` carries only the original closed failure kind
and private diagnostics. Denial/unavailability/cancellation/admission remain distinguishable,
with no receipt disclosure or transient Process retry. This also covers cleanup after
failed lease-admission publication before the application receives its handle. Native
retains cleanup uncertainty even without a prior continuation; HTTP emits the release
flag and omits the continuation. SnapshotRead cannot clear this uncertainty.

Current snapshot reads do not perform owner lookup or publish Waiting. Their
Persistence.Confirmed means the stored snapshot was observed; Stop.SnapshotRead
makes this distinction explicit. Reopened native views can show that authorized
summary, but lack the original captured stream needed for recovery admission and
remain disabled until that actual seam is supplied. This is an explicit activation
gap, not a completed restart feature.
