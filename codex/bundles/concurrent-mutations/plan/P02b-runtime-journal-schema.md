# R1/R2 runtime admission schema refinement

Status: proposed implementation contract, source-reviewed on 2026-10-16. No implementation, build, or execution is claimed by this card. This refines R1/R2 in [the producer cutover plan](P02b-structure-and-runtime-cutover.md). The existing recoverable interactive and Process protocol remains the compatibility baseline.

## Decisions

1. Separate durable **effect admission** from recoverable **provider input**. Use the existing run journal, dispatch lease, proposal IDs, approval bindings, source preparation, and receipt reconciliation provider. Do not persist request-scoped attachment bytes as a workaround.
2. Add a third, explicit session source for owner-admitted detached runs. Its identity is the actual persisted execution run, not a client correlation, purpose enum, fake chat, or fake Process claim.
3. A detached organization's execution scope does not itself grant project access. Retain a bounded immutable projection of the original catalog project permissions, and require original AND current permissions for a first effect. Current read policy governs historical receipt disclosure independently.
4. Persist a new run and its original admission in the existing `GenericNewRunCommitJournal` or same-source reservation. Do not save a naked run and attach authority afterward. Do not hold an external catalog read lease while calling the same store's save method: that self-deadlocks.
5. Preserve the existing default/manual versus automatic approval behavior. Automatic approval approves a specific saved proposal; it does not relax either original permissions, current source authority, or the owner revision/lifetime check.

## Actual ownership and producer evidence

Paths below are repository-relative. Method names are the stable anchors; line numbers can move during this bundle.

| Owner/source | Confirmed behavior | Required change |
| --- | --- | --- |
| `src/MAF/Common/CanDoItAll.AgentFramework.Models/Execution/AgentToolAdmissionModels.cs`, `AgentToolSessionReference` | Chat+authority or background binding, exclusively; no detached branch. | Add explicit detached source with closed validation; leave old JSON interpretation intact. |
| Models `AgentToolJournalModels.cs`, `Validate` | Schemas 1/2/3/4; non-Recoverable support forbids segments/batches. Segment restart checkpoint is required; approvals imply an SDK approval checkpoint. | Version 5 represents the new source/support forms and makes checkpoint capability explicit. |
| Core `ToolPolicy/AgentToolAdmissionJournal.cs`, `BeginSegmentAsync`, `AdmitBatchAsync`, `RequireCurrent` | Effect admission requires Recoverable. Current identity checks assume background or interactive. Receipt-only lease is specifically tied to cancelled runs. | Split effect/provider capabilities and add explicit detached identity checks and receipt-only recovery for an effect-only interrupted run. |
| Core `Execution/AgentFrameworkWorkspaceExecutionService.Helpers.cs`, `AdmitInteractiveRun` | Real chat admission exists for request-scoped input, but support is RequestScopedInput. | New admissions select EffectJournalOnly when provider input cannot be recovered. |
| Core `Execution/AgentFrameworkWorkspaceExecutionService.BackgroundTools.cs`, `AdmitBackgroundRunAsync` | Registered Process source gets admission; unknown/non-Process source returns unchanged. | Preserve Process verification; add detached owner admission at the actual non-chat launch seam. |
| Core `Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`, `ExecuteRunCoreAsync`, `ExecuteSameSourceRunEntryAsync` | Both create a real `ExecutionRunRecord.Id` before provider execution. Detached runs are persisted; same-source reservations already exist. | Supply the initial detached journal in those existing save/reservation candidates. |
| Persistence `Storage/FileSandboxWorkspaceStore.BackgroundReservation.cs` | Both new candidate and prior acknowledged source runs explicitly require Recoverable. | Accept schema-5 EffectJournalOnly with the same exact Process binding; unresolved effects still block replacement. Changing MAF alone is insufficient. |
| Persistence `Storage/FileSandboxWorkspaceStore.cs`, `SaveExecutionRunDetailCoreAsync`, `ReserveExecutionRunAsync` | New run and projections are committed through a durable generic new-run commit journal; save/reservation holds the store semaphore and cross-process workspace lock. | Validate detached initial permission evidence under that existing lock before preparing the commit journal. |
| Persistence `Storage/FileSandboxWorkspaceStore.CatalogReadLease.cs` | Catalog read lease holds that same semaphore and workspace lock until disposed. | Never nest save/reserve under an external lease on the same organization store. |
| MAF `Runtime/MafAgentExecutionAdapter.cs`; `Runtime/Admission/MafToolRunContext.cs` | One Recoverable flag controls protocol wrapping and SDK checkpoint restore. Input attachments are prepared before recovery. `OpenAsync` decodes restart/approval checkpoints. | Split capabilities; reconcile effects before opening missing attachment files or contacting a provider. |
| Workbench `AgentTools/ProjectDefinitionToolAdmission.cs`, `ProjectDefinitionToolProposalCodec.cs`; `ProjectStructure/ProjectDefinitionGovernedBindings.cs` | Exact saved source, operation epoch and prepared proposal already exist. Agent encoding currently requires InteractiveChat; Process uses verified dispatch. | Add detached encoding/verification without changing existing version-1 Agent/Process bytes. |
| Workbench `ProjectStructure/ProjectAgentSourceMutationAuthority.cs` | Catalog lease protects current source through transaction; original/current project flags and exact lifetime grants are intersected. Organization/project governance is checked separately. | Reuse this writer authority with the detached original projection; no replacement authorization subsystem. |
| Web `Api/AgentsApi.cs`, `StartAgentExecutionRunAsync`; `AgentEventsApi.cs` | Ordinary and SSE requests call `ExecuteRunAsync`, including no-chat and auto-approve requests. | Same owner admission, no client-provided authority field. |
| Modules.AgentFramework `Hosting/ScenarioHarnessSupport.cs`, `RunScenarioAsync` | Uses the organization workspace and an ordinary detached request. | Real R2 proof; source text `scenario-harness` grants nothing. |
| Modules.AgentFramework `Workspace/CurrentProfileAgentFrameworkWorkspaceService.cs`, `ResolveCurrentWorkspaceService`; `AgentFrameworkWorkspaceFactory.cs` | Product execution uses the current organization workspace; activity recovery pins an existing workspace identity. | Derive the admitted organization scope from that owner identity, not request `Context.WorkspaceScope`. |

Process-backed workflow agents continue through the Process source branch. `WorkflowProcessToolAdmission` remains Process-specific. Existing stateless Workflow LLM invocations do not advertise these definition tools; existing Structure Workflow asset/task effects retain their owners. R1/R2 does not invent a new Workflow definition writer.

## R1: minimal version-5 journal shape

Keep support values 0 Recoverable and 1 RequestScopedInput unchanged. Add 2 EffectJournalOnly. Schemas 1–4 retain their existing validation and source encodings; historical RequestScopedInput runs are not upgraded in place.

Extend `AgentToolInvocationSegment` with an explicit closed `AgentToolCheckpointKind` and a nullable restart checkpoint. Proposed values are `ProviderRestart = 0` and `EffectsOnly = 1`. Omit/default the new kind for old serialized segments, so old non-null checkpoints still mean ProviderRestart. Add `OriginDispatchLeaseId` only for schema-5 EffectsOnly segments. This is a live-request continuity fence, not a durable caller or business-intent identity.

| Form | Restart checkpoint | Origin lease | Approval checkpoint / pending approvals | Allowed behavior |
| --- | --- | --- | --- | --- |
| Existing provider-restart segment | Required, unchanged SDK/versioned envelope | Absent | Existing exact pairing remains | Existing verified provider restoration. |
| Schema-5 effects-only segment | Must be absent | Required nonempty ID | SDK checkpoint absent; typed pending approval bindings may be present and must match persisted proposals | Original live lease may admit successive provider response batches. New lease may reconcile or execute an explicitly approved saved restorable proposal, but cannot admit a new provider batch or manufacture an SDK session. |

Schema-5 is a capability superset, not permission to mix arbitrary fields. Interactive, background, and detached source branches each retain their own original input rules. EffectJournalOnly cannot carry a typed runtime attachment snapshot or any fake provider restart envelope. Recoverable schema-5 detached runs may carry a real supported runtime context. Provider-native dispatch records remain legal in schema 5 and keep their existing unresolved-dispatch fence; receiptless hosted tools do not become idempotent because the journal is new.

The segment, response envelope, proposed arguments, source preparation, business intent, approved digest, result, and provider-dispatch evidence remain append-only under `AgentToolJournalTransitions.ValidatePersistence`. Add checkpoint kind and origin lease to the immutable comparison. All existing 32-segment, 512-batch, 64-calls-per-batch, 24-MiB journal, 8-MiB envelope, and 64-KiB SourcePreparation bounds still apply, including cumulative UTF-8 checks. Absent checkpoints contribute zero bytes; do not accidentally skip result/proposal accounting.

Core entry points can stay small:

- Existing `BeginSegmentAsync(lease, checkpoint, continuesSegmentId, ct)` remains provider-restart only.
- Add `BeginEffectSegmentAsync(lease, ct)` for a new live invocation. It requires schema-5 EffectJournalOnly, no unresolved earlier provider dispatch/effect, and persists the exact current lease ID.
- `AdmitBatchAsync` accepts durable-effect capability, then requires the matching segment kind and current origin lease for a new EffectsOnly batch. `RequireRecoverable` remains on provider restoration, not effect persistence.
- Replace the lease's boolean cancellation-only distinction with a closed mode, at least Dispatch, CancelledReceiptReconciliation, and InterruptedEffectReconciliation. A reconciliation lease never authorizes `AdmitBatch`, approval generation, provider dispatch, or a new owner effect. Preserve the existing cancellation API as the cancelled-mode wrapper.
- An explicitly approved saved proposal uses a **dispatch** lease and exact persisted approval binding. It is a separate operation from receipt-only reconciliation. A known Completed proposal is never dispatched again; an Executing/unknown proposal is queried by its original owner operation before retry is considered.

MAF needs two branches, not a new runtime abstraction:

1. Before attachment preparation/provider construction, inspect the saved journal. On interrupted EffectJournalOnly, perform read-only receipt reconciliation through existing `IAgentToolReceiptReconciliationProvider`; return a typed continuation-needed result with unresolved/accepted outcomes. Never call the provider to rediscover the old proposal.
2. For a new live EffectJournalOnly request, use the real live SDK session/input but create an EffectsOnly segment. The same proposal wrapper durably stores exact arguments/source before invoking the tool. Do not serialize the attachment-bearing input/session as a checkpoint.
3. For manual approval, retain `PendingToolApprovalRecord` and the existing batch/intent/digest binding. On an explicit approved-proposal resume, reconstruct only a tool whose original persisted preparation and installed `RestoreAdmission` contract agree; dispatch that exact saved proposal under current authority. This tool-only continuation does not require an SDK approval checkpoint. Other saved proposals whose contract requires missing runtime context remain pending with an explicit input requirement; they are not retried through generic reflection.
4. Once saved effects are settled, starting **new model work** requires a new admitted semantic invocation/input. Do not refresh the epoch, revision, or source of an old prepared effect. While the original live lease/input still exists, ordinary provider continuation is permitted. A reopened run cannot claim to possess those live bytes.

Do not widen `ProjectStructureProcessToolAdmission.UsesJournal` indiscriminately: it is shared with non-definition Process tools. Give the three definition tools a durable-effect predicate based on the verified journal source/support. Audit other providers' existing predicates and preserve their behavior until their effect-only source restoration is supported. The global MAF journal still records non-definition calls and uncertainty; this is not authorization to auto-retry them.

## R2: detached source and original permission ceiling

Add optional `DetachedSource` to `AgentToolSessionReference`. Exactly one source form is legal:

- Interactive: nonempty ChatSessionId + AuthorityId, neither background nor detached.
- Background: background binding, empty chat and authority IDs, no detached.
- Detached: detached binding, empty chat ID, authority ID equal to the binding's server-created original governance ID, no background.

Use schema 5 for detached journals. Do not infer the source form from Purpose: manual detached execution currently maps from Unspecified to InteractiveChat in capability composition, while automatic execution uses AutoApprovedNonInteractive. Preserve those inventory/approval choices; source identity is now explicit and independent.

Proposed `AgentToolDetachedSourceBinding` version 1 carries:

| Field | Source and reason |
| --- | --- |
| `SchemaVersion = 1` | Closed interpretation; unsupported versions fail before dispatch. |
| `AgentExecutionGovernanceSnapshot Governance` | Existing type, owner-created once. Agent/profile/generation and authority ID match the session. Scope is the **actual** organization workspace for ordinary product launches; never a request context claim. ReadAllowed reflects an active executable original agent; MutationAllowed is bounded by original CanUseTools. |
| `AgentToolDetachedProjectCeiling ProjectAccess` | New immutable projection of the existing Models-owned `AgentProjectStructureAccessSettings`: CanRead, CanWrite, CanWriteNonTaskStructure, CanWriteTasks, CanCreateProjects, CanCreateSubprojects, AllowAllProjects, immutable allowed project IDs and exact profile/project/lifetime tuples. No arbitrary ConfigurationJson. |
| `CatalogDataRevision ObservedCatalogRevision` | Captured from the actual owner catalog for diagnosis and initial consistency validation, not a global forever-current execution precondition. |
| `DateTimeOffset AgentCreatedAtUtc` | Original catalog entry evidence; require the current same-ID entry to retain it. This is not claimed to replace a future explicit agent lifetime token. Dataset activation and current profile/generation fencing remain required. |
| `bool OriginalCanUseTools` and original approval policy booleans | Preserve the original permission/approval ceiling. The existing owner-persisted run's AutoApprovePendingToolCalls is still explicit operator behavior; an approval setting change never grants project permissions. No secrets or allowed-secret values. |

Bound project ID/lifetime collections to 1024 each, matching the existing governed definition source encoder. Apply the existing 64-KiB SourcePreparation hard ceiling before serialization/copy; the count ceiling does not guarantee a 1024-entry projection fits. Oversized original evidence produces explicit admission validation failure, never truncation. Record actual projected size in owned-fixture validation before cutover. Do not retain the whole agent configuration, instructions, provider credentials, attachment content, or live catalog/Process leases.

`AllowedOperations`/`AllowedCapabilityKeys` on existing governance use **empty = unrestricted dimension**, not deny-all. Do not repurpose empty sets as a detached deny signal. Organization scope plus CanUseTools alone does not admit a project write: the immutable original project projection and current owner grant must both authorize that exact domain and lifetime. Narrowing request context can still remove tools; it never adds original grants.

Keep this projection scoped to the Projects definition cutover. The journal provides durable identity for all attached tools; it does not claim every other domain now has a frozen owner permission projection. Existing non-Projects owners continue their current source/approval checks, and their stronger original ceilings belong to P04/P05/P07. Do not silently remove supported tools when adding the session branch.

### Atomic initial persistence without a second authority store

The smallest integration uses existing save/reservation operations with additional **new-run-only** validation:

1. Core prepares the candidate from the current owner catalog snapshot, actual activity workspace identity, initial run ID and frozen original permissions. No provider/tool effect has happened. A short catalog lease may be used to capture coherent evidence, but it is disposed before save/reserve on the same store.
2. `FileSandboxWorkspaceStore` takes its existing gate and cross-process lock, loads its canonical catalog, and validates the detached candidate's immutable permission projection and agent identity against the actual catalog entry. Profile/workspace identity must match the store/owner's bound identity. An intervening relevant policy change returns a typed stale-preparation result before the generic commit journal is written. Compare the bounded material projection, not just a timestamp; unrelated catalog changes need not abort admission.
3. The existing new-run commit plan writes run+journal+index/projection as one recoverable unit. Same-source reservation returns an already stored original run unchanged; it cannot replace its admission with the candidate's new projection. The existing bounded preparation retry may repeat only before initial admission/provider dispatch.
4. Recovery of an already prepared generic file commit uses its frozen validated payload. Do not re-admit old permission evidence as new authority or make receipt readability depend on today's write permission. Subsequent effects always revalidate current authority anyway.
5. Profile generation is checked before capture and after persistence/before runtime construction. A profile switch may leave a safely admitted but undispatched record; it cannot authorize dispatch in the replacement profile.

The product launch paths enumerated above resolve the organization workspace, so their catalog and execution persistence share this lock. A lower-level non-organization workspace caller must not receive an invented organization grant. Its exact owner scope must either supply an already verified project admission through the existing path or retain a bounded local scope. Confirm such a caller with a source test before enabling project-definition mutations there; this is a lower-level compatibility gate, not a reason to disable the ordinary detached API/SSE/scenario paths.

No new authority registry is required. Extend the existing `IAgentExecutionAuthorityResolver` with a typed detached observation/revalidation operation if Modules-level current catalog policy is needed by the journal, or pass its typed delegate at `AgentFrameworkWorkspaceFactory` composition. Do not dispatch this through `SourceKind` strings. Its result is current read/dispatch permission for the original detached binding, not a replacement original snapshot. The authority ID remains stable on resume.

### Workbench and other session consumers

`ProjectDefinitionToolAdmission.CaptureAsync` currently captures the live catalog grant at provider composition. For a detached run, intersect that live projection with the saved original ceiling before writing SourcePreparation; never replace the original ceiling on restart. Saved proposals continue to use their original envelope through `RestoreAdmission`.

Add a domain-separated detached caller encoding, for example `projects:detached-agent-caller:v1`, containing the fixed profile ID, agent ID, execution run ID, and original authority ID. Add a detached material effect encoding that binds original governance, project ceiling, exact proposal, operation domain and any creation reservation. Keep existing interactive `projects:agent-caller:v1` and Process bytes unchanged. Effect kind can remain Agent with an explicitly supported new effect encoding version; changing it requires codec version tests, not CLR serialization.

The Projects receipt reader continues to check current target lifetime/availability before current target access. Detached source read checks current profile/generation, exact saved run/source, current active catalog actor and current target CanRead/lifetime grant. Write revocation or expired dispatch alone cannot hide an accepted receipt. Source-read revocation must hide it, including a stored Completed result; a cached tool result is not an authorization bypass.

Every `BackgroundSource == null` assumption needs an explicit branch audit. Concrete consumers found in source:

- Core `AgentToolAdmissionJournal`, `AgentToolAdmissionJournal.Background`, `AgentPendingApprovalCancellation`, execution continuation guards and receipt reconciliation.
- Workbench `ProjectDefinitionToolAdmission`, proposal codec/governed binding, `ProjectStructureResultDisclosureService`, `ProjectStructureProcessToolAdmission` and its Background partial, `ProjectProcessAssetToolAdmission`.
- Modules.AgentFramework `ImageGenerationResultDisclosureService`, `WorkspaceToolResultSource`, `WorkflowProcessToolAdmission`, and `Workspace/AgentAssetCheckpointQuery`.
- CRM/HR `CrmPlanningProposalPreparer`; Models `WorkflowProcessToolBinding`.

Process-only consumers stay explicitly Process-only. General result-disclosure consumers must use a verified detached source-read branch rather than trying `FromCaptured` with a fabricated TurnContext. Audit each against its existing supported detached behavior; do not treat adding `DetachedSource` to JSON as completion.

## Implementation sequence and broader proof inventory

The [ordered buildable checkpoints](#ordered-buildable-checkpoints) below are the
single authoritative implementation sequence, including scopes, dependencies and
validation gates. Their [R1a proof target](#r1a-implementation-details-and-exact-proof-target)
defines the first checkpoint's exact existing source counts and proposed additions.

Deterministic proposed test names, to turn into exact discovery counts when source is added:

- `Journal_v1_to_v4_keep_their_original_checkpoint_and_source_interpretation`.
- `Effect_only_segment_rejects_provider_checkpoint_and_replaced_origin_lease`.
- `Effect_only_provider_native_dispatch_uncertainty_blocks_new_work`.
- `Effect_only_source_support_and_approved_payload_cannot_change_after_persistence`.
- `Request_scoped_definition_acceptance_is_reconciled_before_attachment_open_or_provider_call`.
- `Request_scoped_manual_approval_dispatches_only_the_saved_restorable_proposal`.
- `Process_effect_only_reservation_reuses_same_claim_and_blocks_unresolved_prior_claim`.
- `Detached_new_run_commit_recovers_one_matching_run_and_original_admission`.
- `Detached_catalog_change_between_capture_and_save_rejects_before_provider_dispatch`.
- `Detached_same_source_reuse_does_not_replace_original_ceiling_or_operation_identity`.
- `Detached_source_metadata_cannot_select_chat_Process_or_organization_authority`.
- `Detached_original_read_only_or_project_limited_grant_cannot_be_widened_by_later_configuration`.
- `Detached_autoapproval_does_not_override_original_or_current_project_permission`.
- `Detached_write_revocation_blocks_new_effect_but_current_read_allows_exact_receipt`.
- `Detached_read_revocation_blocks_cached_completed_result_and_receipt_disclosure`.
- `Detached_definition_create_update_and_subcreate_use_real_run_identity_after_restart`.
- `Legacy_request_scoped_or_detached_run_cannot_acquire_fabricated_original_intents`.

These broader launch/recovery test names remain proposed, not authored or passing
proof. The signatures and compatibility constraints below are settled design
decisions; implementation has not started.

## Settled continuation and current-source signatures

### Existing approval and recovery APIs remain the public entry points

Keep the existing signatures and HTTP routes:

```csharp
Task<ExecutionRunResult> ContinueExecutionRunAsync(
    Guid executionRunId,
    AgentExecutionOperationId activityOperationId,
    IReadOnlyList<PendingToolApprovalDecision> decisions,
    bool autoApprovePendingToolCalls = false,
    CancellationToken cancellationToken = default);

Task<ExecutionRunResult> RecoverExecutionRunAsync(
    Guid executionRunId,
    AgentExecutionOperationId activityOperationId,
    CancellationToken cancellationToken = default);
```

`POST /api/agents/execution-runs/{id}/pending-approvals` and its streaming variant already provide exact approval IDs. `PendingToolApprovalRecord.ToolAdmission` binds each server-persisted approval to batch, intent, semantic version and digest; `AgentToolJournalTransitions.ApplyDecisions` resolves that original binding and rejects changed supplied bindings. `PendingApprovalStateMatches` fences the run snapshot under its atomic writer. Therefore a new endpoint or client-supplied authority/request body is unnecessary.

Retain explicit decision-ID exact coverage and the existing legacy approve-all request behavior. The older form deliberately approves the current pending set, as already documented; it must never cause that set to be regenerated from current model output. The mapper currently does not carry a ToolAdmission field over HTTP, so the binding must come from the original **stored** pending record under the same atomic decision transition, not be reconstructed from a new proposal. Decision text, current source settings and later auto-approve flags cannot replace its digest.

Add an early schema-5 EffectJournalOnly branch in `ContinueExecutionRunEntryAsync`, before `ResolveAdmittedRuntimeContext`, workflow/SDK checkpoint validation, provider lease/credential resolution, or `ValidateAdmittedResumeAuthorityAsync`'s write requirement. The branch uses the existing run dispatch lease, atomically records the exact decisions, and invokes only saved, approved, explicitly restorable tool proposals. Rejecting a proposal never contacts its owner or the provider. The original saved approval/intent remains after a failure between decision persistence and tool dispatch.

`POST .../{id}/recover` is the existing explicit recovery action. For EffectJournalOnly it first observes original owner receipts under current read policy. It may resume an exact already-approved, self-contained OwnerReceipt proposal only when its installed original `RestoreAdmission` agrees and the receipt still admits that same operation; current dispatch/write authority is checked only for a potential effect. No pending approval is implicitly approved. Known acceptance skips tool invocation. A non-replayable effect or provider-native dispatch uncertainty remains fenced. This keeps the ordinary recovery request useful without adding an unsafe generic retry operation. The cancellation endpoint retains its existing permanently no-dispatch semantics.

The MAF continuation port can continue using `AgentRuntimeContinuationRequest` for a nonempty set of newly decided approvals, with a closed optional `AgentRuntimeContinuationMode` (`Provider = 0`, `SavedEffectsOnly = 1`). The runtime implementation branches on SavedEffectsOnly before constructing/restoring an SDK session. Existing request fields may supply tool composition metadata but do not authorize provider access. Receipt-only recovery stays in Core through `IAgentToolReceiptReconciliationProvider`, so it needs neither an artificial decision nor an artificial `AgentRuntimeContinuationRequest`. A saved already-approved proposal recovery uses the journal's exact proposal dispatcher after current preparation; it does not manufacture a second approval decision. Factor that narrow tool-dispatch routine from MAF's existing admitted tool path rather than creating another tool registry.

### Input-required is additive result data, not successful provider completion

Keep `ExecutionRunResult`, existing `ExecutionState` values, existing `RunOutcome`, and the result mapper. Add a closed `AgentToolContinuationRequirement` enum with `None = 0`, `ProviderInputRequired = 1`, and `EffectReconciliationRequired = 2`, plus an optional/default-omitted `ToolContinuationRequirement` init property on `ExecutionRunResult` and its API response. Persist the same value in the schema-5 journal so a later GET/recover result cannot lose it. Schemas 1–4 require None.

Use `ExecutionState.WaitingOnTool` and no terminal RunOutcome when an effect-only invocation is stopped for either requirement. This does not mean a provider response or whole agent task succeeded. Existing PendingApprovals remain independently visible. EffectReconciliationRequired has precedence while an uncertain dispatched effect/native dispatch remains; ProviderInputRequired applies when no such uncertainty remains but a saved tool or new model turn requires unavailable original input. Do not use `HasUnresolvedEffects` alone to choose the requirement: that property also includes Prepared proposals that have never been dispatched. Source denial remains the existing typed access error, not a fake input problem. UI/API/SSE consumers must display the typed requirement and avoid presenting provider restart as available. The normal activity operation may finish after persisting this waiting result, as it already can when approvals are pending.

Do not fabricate token counts or a provider metric to construct the existing result. Reuse persisted metrics when present; when recovery is the first persisted result, use the existing metric record with actual recovery duration and zero new provider usage. It must not overwrite earlier provider usage observations. The current `LoadExistingExecutionRunResultAsync` assumes a metric exists, so its effect-only branch needs this explicit handling and an interruption-before-first-metric regression.

### One typed operation on the existing authority resolver

Extend `IAgentExecutionAuthorityResolver` with:

```csharp
ValueTask<AgentToolDetachedSourceObservation> ObserveDetachedAsync(
    AgentToolSessionAdmission original,
    CancellationToken cancellationToken = default);
```

`AgentToolDetachedSourceObservation` is a Core record containing `bool ReadAllowed` and `bool DispatchAllowed`. The verified original admission already contains the stable source identity/material projection, so this operation must not return a new authority ID, a new permission snapshot, or a refreshed operation epoch. The default interface method, if needed for existing custom resolvers, throws an explicit unsupported-source exception; it never returns an allow decision. `CanonicalAgentExecutionAuthorityResolver` implements it using the actual organization catalog/current profile and generation. `AgentFrameworkWorkspaceFactory` passes that existing resolver to the journal; do not add a provider registry or select this method by an arbitrary source string.

The journal first verifies the original stored run/session/source/profile, then calls this operation outside its run-store mutation lock. ReadAllowed requires original read admission and the current same active non-template catalog actor, same profile/generation and original owner scope; it does not require current CanUseTools. DispatchAllowed additionally requires original and current CanUseTools. This is only the source-level decision: project target read/write/lifetime grants remain Workbench/Projects policy. A removed current project write grant does not imply ReadAllowed=false. The existing Project source mutation lease still protects the current domain authority through the owner transaction; the detached observation does not replace that lease.

## Independent compatibility and capability review

The following are concrete requirements discovered while challenging the proposed shape. They refine implementation scope; none is a completed source fix.

### Owner receipt evidence cannot masquerade as an SDK result

Current `IAgentToolReceiptReconciliationProvider` returns an `AgentToolReceiptObservation` with opaque owner receipt evidence. Current `AgentToolProposalRecord.Completed` requires a non-null SDK result envelope, and MAF decodes it as its own ResultCheckpoint. Reusing cancellation's `Cancelled` state for an interrupted effect would also be false and would revoke pending approvals. Consequently, R1a needs one explicit schema-5 terminal effect state, `OwnerReceiptConfirmed`, and an optional `AgentToolOwnerReceipt(AgentToolCommittedEffect Effect, AgentToolProtocolEnvelope Receipt)` on the proposal. This is the existing observation pair moved into a Models-owned persisted shape, not a second receipt store.

Only an originally approved, dispatched OwnerReceipt proposal with exact binding may enter that state after current authorized lookup. EffectState must be Committed, Cancellation must be absent, the owner evidence pair must be complete and bounded, and the state/evidence are immutable. The transition does not introduce a Result or DisclosureEvidence. If a real SDK result and disclosure evidence were already saved in ReconciliationRequired, both remain byte-for-byte unchanged as historical evidence; if absent, they stay absent. This refinement was accepted during the 2026-10-16 readiness review: `CompleteInvocationAsync` can already persist a genuine uncertain SDK result, while `ValidatePersistence` prohibits clearing its disclosure evidence. A second historical-result field is unnecessary. Old schemas reject the new state. Such a proposal is never redispatched and neither its owner receipt nor its retained old SDK result is decoded as a successful recovered SDK result. `HasUnresolvedEffects` treats its **effect** as resolved. Its saved receipt is reauthorized on every disclosure. Existing Completed proposals retain their original SDK result and disclosure contract unchanged. The pending run still reports ProviderInputRequired rather than inventing an assistant answer from an owner receipt.

Current generic `NotObserved` receipt observation does not distinguish an open miss from a closed definitive absence. For R1 receipt-only recovery it must remain uncertainty, never become NotCommitted/Rejected. Actual replay eligibility is checked by the existing Projects invocation path using its richer original-operation lookup. No mutation can be admitted solely because the generic reconciliation provider returned NotObserved.

### Session classification must fail closed before detached admission

Detached is selected only by the trusted non-chat execution owner after ruling out a required background admission. `ProcessRunId`, `ProcessStepId`, typed Process metadata, and `ExecutionRunSourceKey.RequiresBackgroundAdmission` cannot fall through to detached merely because a Process source policy is missing or rejects the claim. Explicit required background evidence with no installed/valid owner policy is an admission failure. Ordinary source labels such as manual/scenario-harness remain labels and cannot select a narrower or stronger authority branch. HTTP and direct service tests must cover this distinction.

Do not inject the detached organization governance snapshot into every provider as a universal permission grant. Each provider keeps its present tool availability and owner authorization; the new journal source branch supplies verified identity/read checks only. The Projects original ceiling is consumed explicitly by the definition admission path. General disclosure providers gain a detached source-read branch, while Process-only workflow/asset bindings remain Process-only. A null TurnContext never means authorization can be skipped.

### Material projection validation is exact and new-run-only

Introduce one pure version-1 capture/comparison function in Models for the detached material projection. It reads the existing normalized ProjectStructure access section plus original CanUseTools and approval policy booleans from `AgentDefinition`, copies all sets immutably, and uses ordinal deterministic ordering for encoding. Both Core candidate creation and Persistence initial validation call this same implementation; do not maintain two subtly different permission lists.

Compare every relevant flag, AllowAllProjects, exact ID/lifetime membership, agent ID/birth evidence, and the store's actual workspace/profile binding. Compare governance's derived fields with the expected owner-selected values and policy version/fingerprint; accepting a caller-supplied governance fingerprint is not validation. Collection record equality is insufficient: immutable collection instances may differ after JSON restore, so use set/primitive equality with explicit duplicate and bound validation. Sorting alone must not erase a malformed duplicate supplied in an initial persisted contract.

ObservedCatalogRevision is evidence, not the sole comparison: accepting a forged wider projection merely because its revision matches would be unsafe; rejecting an unrelated catalog edit when material projection is identical would add avoidable launch contention. Permissions changing between capture and save reject before provider dispatch. Thereafter source identity/ceiling stays immutable; current revocation is enforced at dispatch/read rather than by rewriting the old run. Never apply today's catalog equality check to a historical accepted receipt or every later run update.

For file commit recovery, validate the stored plan's internal identity/projection consistency but do not recapture a new original ceiling. Initial atomic validation occurred before the durable generic plan was prepared. Verify recovery ordering in the existing File store with a crash-boundary test; if pending-plan replay can run after an unrelated catalog mutation without prior recovery, retain its original validated projection and rely on current source checks before dispatch. Do not strand a half-committed execution because a later policy changed.

### Non-definition tool compatibility is an explicit gate

Turning on a journal globally for detached/effect-only runs can activate metadata and result-disclosure code that previously took an unjournaled branch. A source search for BackgroundSource covers only direct branch checks; tests must also cover runtime wrappers and feature predicates that use AdmittedToolSession or Recoverable. The existing default approval policy, sensitive disclosure's explicit approval requirement, read tools' owner permissions, provider-native uncertainty and non-idempotent ReconcileBeforeRetry behavior must remain intact.

Live original requests may use their currently supported attached tools. After losing non-durable input, only a saved tool with an explicit original restoration contract may be executed without that input; other proposals stay pending with the typed requirement. This is an honest recovery limit, not silent tool removal. Each affected owner must prove its previous live behavior still works, and must not gain automatic restart merely because the journal exists. The general consumer audit in this card is mandatory before R3 removes legacy definition fallbacks.

Additional regression cases required by this review:

- `Existing_approval_endpoint_binds_effect_only_decisions_to_original_stored_proposals`.
- `Effect_only_recovery_before_first_metric_does_not_invent_provider_usage_or_complete_the_task`.
- `Owner_receipt_confirmation_is_terminal_and_is_never_decoded_as_an_SDK_result`.
- `Generic_receipt_not_observed_does_not_prove_absence_or_authorize_retry`.
- `Required_Process_admission_failure_cannot_fall_through_to_detached_authority`.
- `Detached_material_projection_roundtrip_uses_set_equality_and_rejects_wider_flags_or_lifetimes`.
- `Detached_unrelated_catalog_edit_does_not_change_the_original_material_projection`.
- `Detached_file_commit_recovery_retains_original_admission_across_later_policy_revocation`.
- `Detached_journal_preserves_existing_workspace_image_CRM_and_sensitive_disclosure_policies`.

The three public/composition signature decisions are now closed. Remaining blockers are implementation/proof gates: the owner-receipt state must be included in the schema review; actual live non-definition tool compatibility must be demonstrated; and any real non-organization detached producer must have an explicit bounded owner-source contract before it can perform Projects definition writes. The ordinary organization API/SSE/scenario producers are confirmed and do not need a new identity service. No production cutover is authorized or proven by this document alone.

## Ordered buildable checkpoints

Readiness refresh: 2026-10-16, read-only source review. These checkpoints replace a
single broad R1/R2 implementation step. Every checkpoint closes its own build,
focused proof and portability-static record before the next production step.
No checkpoint below has been implemented or executed by this review.

| Checkpoint | Smallest production set and resulting boundary | Exit proof before the next checkpoint |
| --- | --- | --- |
| **R1a: journal schema and immutable effect truth** | Models `Execution/AgentToolJournalModels.cs` and `AgentToolProviderDispatchModels.cs`; Core `ToolPolicy/AgentToolJournalTransitions.cs` and the existing claim's explicit terminal-state guard; narrow MAF execution/continuation and checkpoint-decode guards. Add schema 5, support value 2, checkpoint kind/origin, continuation requirement and OwnerReceiptConfirmed. Existing factories still emit their current versions/support. An explicit EffectJournalOnly value reaching a runtime adapter is refused before provider construction until R1d; it cannot take today's unjournaled `!Recoverable` path. | Exact first-checkpoint proof below. Old wire shapes/enum numbers remain unchanged; no new producer and no fallback removal. |
| **R1b: live effect journal and read-only reconciliation** | Core `AgentToolAdmissionJournal.cs`, `.Background.cs`, `.ProviderDispatch.cs`, `AgentToolReceiptReconciliation.cs` and its existing lease type. Add BeginEffectSegmentAsync, origin-lease validation, explicit lease modes, exact interrupted-owner receipt confirmation and current receipt disclosure. Keep provider-restart methods separate. | Independent file-store dispatch serialization; same lease can append, replacement lease cannot append a fresh model batch; receipt lookup outage/miss remains uncertain; found receipt is terminal even after write revocation; current read revocation refuses disclosure. Lost file-commit acknowledgment reuses the original intent. |
| **R2a: detached identity and atomic initial admission** | Models source/projection types and pure capture/compare; Core actual launch capture and existing authority-resolver observation; Persistence `FileSandboxWorkspaceStore` initial save/reservation under its existing lock; Modules.AgentFramework resolver/factory composition. Keep ordinary producer activation gated until R1d/R2b. | Same-ID catalog replacement, changed original permission field/set/lifetime and required Process evidence fail before dispatch; unrelated catalog edits do not. Generic new-run commit recovery retains original validated admission. No nested catalog-read/save lock, new registry or client authority. |
| **R2b: Projects definition source restoration** | Workbench `ProjectDefinitionToolAdmission`, `ProjectDefinitionToolProposalCodec`, `ProjectDefinitionGovernedBindings` and the existing Agent source authority. Give only the three definition tools their own durable-effect predicate. Add a versioned detached source encoding while retaining version-1 Agent/Process interpretation and original operation/reservation fields. | Actual create/update/subcreate each survive restart with original identity; accepted replay after source expiry/current write revocation; current read denial; first effect still needs current write authority and original target lifetime/revision. Process admission failure never becomes detached. |
| **R1c: continuation result and persistence plumbing** | Core ExecutionRuns/ContinuationGuards/Helpers and pending cancellation; Models ExecutionRunResult; Web `AgentApiResponseContracts` mapper/SSE result; startup recovery and agent/scenario surfaces. Add the closed requirement, exact stored approval application, no-metric recovery handling and honest WaitingOnTool persistence. | REST/SSE/GET expose the same requirement; interruption before first metric records no invented provider usage; cancellation retains no-dispatch behavior; approval decisions bind the saved proposal after restart. Core receipt recovery runs before provider/input preparation. |
| **R1d: actual MAF effect-only execution** | MAF execution/continuation adapters, `MafToolRunContext`, capability composition and original saved-proposal dispatcher. Replace R1a's temporary explicit unsupported guard with tested effects-only behavior. No SDK checkpoint is fabricated, decoded from an owner receipt, or restored from missing live attachment bytes. | Live attachment-bearing manual/automatic requests retain their tools; same live lease can continue; new lease cannot recreate missing input. Exact explicitly restorable approved proposal may execute without provider access. Non-restorable proposals return the typed requirement. Provider-native uncertainty remains fenced. |
| **R2c: consumer compatibility and producer activation** | The consumer table below, then actual interactive request-scoped, ordinary detached API/SSE/scenario and Process launch selection. Enable only after every source/approval/disclosure path in that table has an explicit decision and proof. | Owner-specific live compatibility, current-read restoration and source classification tests; no implicit capability widening from a non-null journal/governance or null BackgroundSource. Same-source Process reservation preserves the old admitted execution instead of preparing a new operation. |
| **R3: remove remaining definition fallbacks** | Only after R2c, delete the create/update/subcreate legacy branches in `ProjectStructureAgentRuntimeToolProvider.ProjectStructureToolBuilder` and remove now-unused legacy forwarding when its separate callers have migrated. | Source search finds no direct or indirect definition producer bypass; actual interactive, detached, governed Process and workflow-backed Process paths use protected operations. Required live-path tests run before removing compatibility branches. H1 structure HTTP and native starter/subproject cutovers retain their separate owners/cards. |

R2a and R1c can be prepared independently after R1a, but any shared Models/Core or
standard build outputs remain serialized. R1d needs R1b and R1c; enabling detached
definition tools also needs R2a/R2b. There is no intermediate release that selects
EffectJournalOnly while its only execution path is unjournaled or provider-only.

### R1a implementation details and exact proof target

Do not change `CurrentSchemaVersion = 1` to 5 globally. Add a separate schema-5
constant; preserve the existing schema-2 background, schema-3 provider-dispatch
and schema-4 typed-input interpretation. Append enum values rather than
renumbering them. Omit new default/null JSON fields so existing stored envelopes
and reference fixtures remain byte-stable. Existing RequestScopedInput journals
cannot acquire segments or be upgraded into EffectJournalOnly by changing support.

The schema-5 validator must check source/support/checkpoint combinations explicitly:
absent EffectsOnly restart/approval envelopes, nonempty original lease, exact
pending-approval bindings, and no retained attachment snapshot for effects-only
input. A ProviderRestart segment still requires a real checkpoint and no effect
origin lease. Preserve the existing background input restrictions; schema 5 alone
does not authorize a new background attachment format. Provider-dispatch validation
currently permits records only in schemas 3/4 and therefore must explicitly allow
5 without relaxing its exact native approval/claim/response binding rules.

OwnerReceiptConfirmed is appended after existing proposal state values. Its model
validation requires schema 5, an approved exact digest, a nonempty original dispatch
claim, mutation + OwnerReceipt recovery, no ProviderCall, Committed effect state,
complete bounded owner evidence and no Cancellation. The transition is allowed
only from Executing/ReconciliationRequired of that exact proposal. It must retain
the prior Result and DisclosureEvidence exactly and then make both, the claim and
owner receipt immutable. Prepared, Rejected, Cancelled and Completed cannot be
converted into receipt-confirmed execution. New owner receipt bytes contribute to
both cumulative UTF-8 and final serialized journal bounds even when there is no
restart checkpoint. A receipt confirms an effect, not a completed agent task.

First-checkpoint proposed new Unit file:
`tests/Unit/CanDoItAll.Tests.Unit/AgentToolJournalSchemaFiveTests.cs`.
The following **18 planned cases** are a test contract, not a source/discovery count:

- `Legacy_journals_preserve_wire_shape_and_restart_semantics`: four schema rows 1–4.
- `Legacy_schema_refuses_effect_only_fields_and_owner_receipt_terminal`: four schema rows 1–4.
- `Effects_only_checkpoint_accepts_only_the_live_origin_and_no_provider_envelopes`.
- `Effects_only_saved_approval_requires_exact_existing_proposal_binding`.
- `Owner_receipt_confirmation_roundtrips_without_a_provider_result`.
- `Owner_receipt_confirmation_preserves_an_existing_unknown_result_and_disclosure`.
- `Owner_receipt_confirmation_requires_original_approved_dispatched_owner_receipt_effect`.
- `Owner_receipt_and_prior_result_are_immutable_after_confirmation`.
- `Effect_segment_kind_origin_response_source_and_binding_are_append_only`.
- `Receipt_confirmation_cannot_resurrect_rejected_completed_or_cancelled_proposals`.
- `Missing_restart_checkpoint_does_not_bypass_whole_journal_bounds`.
- `Schema_five_provider_dispatches_keep_native_uncertainty_and_response_fences`.

Proposed new Integration file:
`tests/Integration/CanDoItAll.Tests.Integration/AgentToolEffectOnlyActivationBoundaryTests.cs`,
two rows for execute and continuation, proving an explicit effects-only request
cannot enter the old unjournaled/provider branch before R1d. Reuse the existing
fake provider and file-store fixture; no database/container is needed. These are
**two planned cases, zero authored cases at this readiness checkpoint**.

Current source counts were enumerated from Facts and InlineData (no dynamic data
in these classes):

| Assembly/filter class | Existing source cases | R1a purpose |
| --- | ---: | --- |
| Unit `AgentToolDisclosureEvidenceTests` | 5 | Existing result evidence and absent-field wire compatibility. |
| Unit `AgentToolBackgroundSourceSerializationTests` | 5 | Original interactive/background identity JSON. |
| Unit `AgentPendingApprovalCancellationTests` | 13 | No-dispatch cancellation and original proposal bindings. |
| Integration `AgentToolAdmissionJournalIntegrationTests` | 14 | Interactive current lease, file commit loss, immutable batch, exact approvals. |
| Integration `AgentToolBackgroundJournalIntegrationTests` (both partial files) | 34 | Existing Process source, reservation, result/disclosure and recovery semantics. |

Thus the **current existing source total is 71** (23 Unit + 48 Integration).
After the proposed 20 cases are authored, the planned first-checkpoint total is
**91** (41 Unit + 50 Integration); regenerate source counts and require discovery
to match before execution. Do not report 91 as discovered now.

Exact proposed filters, separately in the standard Unit and Integration projects:

```text
FullyQualifiedName~AgentToolJournalSchemaFiveTests|FullyQualifiedName~AgentToolDisclosureEvidenceTests|FullyQualifiedName~AgentToolBackgroundSourceSerializationTests|FullyQualifiedName~AgentPendingApprovalCancellationTests
```

```text
FullyQualifiedName~AgentToolEffectOnlyActivationBoundaryTests|FullyQualifiedName~AgentToolAdmissionJournalIntegrationTests|FullyQualifiedName~AgentToolBackgroundJournalIntegrationTests
```

Parent-owned build order is Models/Core/MAF through the existing standard Release
project graph, then the existing Unit and Integration projects; reuse their normal
bin/obj outputs. Affected tests plus portability-static are the R1a gate. Native
provider-loop, hosted tools, generic new-run commit recovery and package/deletion
tests are required at the later checkpoints that activate their behavior rather
than repeatedly running unrelated lanes during the model-only step.

### Consumer audit required before producer activation or fallback removal

The source inventory used `AgentToolAdmissionSupport`, `AgentToolProposalState`,
`RestartCheckpoint`, `AdmittedToolSession`, `BackgroundSource`,
`HasUnresolvedEffects`, `HasUnresolvedProviderDispatch` and
`AgentToolJournalTransitions`, then followed runtime composition, storage and
result-mapping callers. Table paths are relative to their named project and name
the concrete seam requiring a decision, not a generic promise to audit later.

| Consumer family and exact source anchors | Required decision/proof before R2c/R3 |
| --- | --- |
| Models `AgentToolJournalModels`, `AgentToolProviderDispatchModels`; Core `AgentToolJournalTransitions` | New state is resolved effect truth, not SDK completion. Immutable existing result/disclosure and cancellation/provider-native semantics survive roundtrip and attempted overwrite. |
| Core `ToolPolicy/AgentToolAdmissionJournal` and its Background/ProviderDispatch partials; `AgentToolReceiptReconciliationClaim.RequireOwner` | Split Dispatch/read-only lease modes. Current claim code requires cancellation-only reconciliation; new mode must not permit ClaimInvocation, provider dispatch, approval creation or AdmitBatch. Preserve current-read revalidation for both new and cached receipt evidence. |
| Core Execution service `Helpers.AdmitInteractiveRun`, `BackgroundTools.AdmitBackgroundRunAsync`, `ExecutionRuns.ExecuteRunCoreAsync/ExecuteSameSourceRunEntryAsync`, Chat entry | Select support only at a new actual admission; reject required Process evidence before detached selection. Existing historical support/source remains fixed. |
| Core ExecutionRuns continuation/recover/cancel and `ContinuationGuards` (both whole-workspace and split-store branches); `AgentPendingApprovalCancellation` | Apply stored decisions under the atomic writer in both storage paths; do not require provider restart or current write access merely to discover a prior accepted receipt. Cancellation must remain no-dispatch. Handle no persisted metric explicitly. |
| MAF `MafAgentExecutionAdapter`, `MafAgentContinuationAdapter`, `MafStreamingTurnExecutor`, `MafRuntimeAgentFactory`; `Admission/MafToolRunContext` and `MafToolAdmissionChatClient` | Their Recoverable booleans currently control journal wrapping, native protocol and SDK decode. Replace each by its actual capability check; fail closed instead of silently selecting an unjournaled branch. Completed replay and receipt-confirmed effect recovery stay distinct. |
| MAF `Capabilities/RuntimeCapabilityComposer`, `.RuntimeToolProviders`, `RuntimeToolProviderComposer`; Tooling `AgentRuntimeToolProviderContext` and Models runtime options | Propagation alone does not authorize a source. Preserve Process-only native ToolBinding restrictions and existing provider/tool metadata fingerprint checks. No synthetic governance/context to satisfy another provider. |
| Workbench `ProjectDefinitionToolAdmission.UsesJournal`, `ProjectDefinitionToolProposalCodec`, `ProjectDefinitionGovernedBindings`; `ProjectStructureAgentRuntimeToolProvider` | Stop inheriting the broad Process-tool predicate for definition effects. Explicit original Agent/Process/detached restoration, separate source witness and caller scope, exact original reservation/target/operation. Remove fallback only after real producer coverage. |
| Workbench `ProjectStructureProcessToolAdmission` + Background partial; `ProjectProcessAssetToolAdmission`; `ProjectStructureWorkflowAuthority.ProcessTools`; `Workbench/ProjectProcessAssetContribution` | These are not definition adapters. Keep Process-specific task/asset/workflow admission as such; `BackgroundSource == null` is no longer proof of an interactive source. Add detached support only where the existing owner authority actually accepts that source. |
| Workbench `ProjectStructureResultDisclosureService` | Its blanket Recoverable predicate and interactive fallback require explicit source branching, including read-after-write-revocation. `Handles` must still exclude specialized definition/process asset receipt adapters exactly once. |
| Modules.AgentFramework `AgentTools/WorkspaceToolResultSource`, `ImageGenerationResultDisclosureService`; MAF `WorkspaceToolResultDisclosure`, `MafSkillResultDisclosure` | Enable existing live read/result behavior with the proper durable-effect predicate. Original workspace/resource evidence and current read policy remain required. Missing TurnContext does not imply allow, and detached organization scope does not become a project scope. |
| CRM/HR `CrmPlanningToolPolicy`, `CrmPlanningProposalPreparer`, `CrmPlanningAgentRuntimeToolProvider`; Integration SimpleChats `HrSimpleChatRuntimeToolProvider` and `HrSimpleChatRuntimeAuthorization` | CRM currently tests InteractiveChat purpose + null BackgroundSource; HR tests purpose + non-null governance/session. Detached must not satisfy those interactive source rules accidentally. Preserve managed-actor, project-scope and sensitive explicit-approval policy. HR operations currently unavailable for non-Recoverable input stay unavailable unless their own exact effect-only restoration is implemented and tested. |
| Integration Storage `StorageAgentRuntimeToolProvider`; Memory `MemoryAgentRuntimeToolProvider`; Scheduler `SchedulerAgentRuntimeToolProvider` | Storage/Memory result paths call RequireSessionAsync and acquire current catalog grants; new source observation must retain those grants and not imply automatic retry. Scheduler passes governance/session to existing Structure authority; automatic detached governance must not silently widen schedule mutations. |
| Modules.AgentFramework `AgentTools/WorkflowProcessToolAdmission`, `WorkflowAgentRuntimeToolProvider`, `Persistence/PersistentWorkflowStores`; Processes `ProcessToolBackgroundSourcePolicy`, `ProcessExecutionClaimEvidenceReader`, `ProcessesModuleServiceCollectionExtensions` | Preserve Process-only workflow invocation and exact claim source. ClaimEvidenceReader currently recognizes failed recoverable background runs only; effect-only waiting/recovery needs an explicit owner decision before enabling Process producers. No fallback from a failed Process observation to detached. |
| Persistence `FileSandboxWorkspaceStore.BackgroundReservation`, initial save/reservation + GenericNewRunCommit; Core `WorkspaceStoreConcurrency`; Persistence `FileSandboxWorkspaceExecutionSliceStore` | New-run admission validation occurs under the existing lock. Whole-state replacement cannot drop a journal or add one to a legacy run. WaitingOnTool still blocks session/deletion even when a receipt resolves the effect. Same-source reservation never substitutes a new permission ceiling. |
| Modules.AgentFramework `Execution/AgentFrameworkExecutionRecoveryService`, `Workspace/AgentAssetCheckpointQuery` | Startup currently preserves only Recoverable and otherwise cancels; add explicit effect-only preservation before activation. Asset Observe currently recognizes Completed/CancelledReceipt only: decide OwnerReceiptConfirmed as committed only for its supported exact Process asset proposal. Keep cancellation-specific receipt endpoints cancellation-specific. |
| Core `Packages/AgentPackageImportService`; Persistence `Packages/ZipAgentPackageService`; execution deletion services | Existing export strips every ToolAdmission and import refuses executable journals regardless of support. Preserve these generic guards. New receipt state must not make a waiting run safe to delete or export as executable history. |
| Web `AgentApiResponseContracts.AgentApiResponseMapper`, `AgentsApi`, `AgentEventsApi`, `Streaming/AgentActivityServerSentEventWriter`; Modules.AgentFramework `AgentChatExecutionOrchestrator`, `AgentChatPanel`, `ScenarioHarnessPanel`, floating chat coordinator/close dialog | Forward and render the typed continuation requirement. Exact approval and recovery endpoints retain stored IDs; waiting for new input is not a successful provider response. Startup/reopen/chat restore must not offer a fake provider restart or clear unresolved history. |
| Product workspace wrappers/factory, `ScenarioHarnessSupport`, ordinary API/SSE launch and Processes/workflow dispatch producers | Verify actual owner workspace/profile/run identity for each path. Scenario/source text or client Context.WorkspaceScope never becomes authority. Lower-level non-organization entry points remain a separately proven source contract. |

The unrelated `Modules.Security/SecretMigration` BackgroundSource text match is
not an Agent tool journal consumer. No conclusion above relies on that false
positive. This audit is implementation readiness, not proof that the listed
consumer changes or compatibility tests already exist.
