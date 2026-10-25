# Structure HTTP and remaining runtime definition cutover

Status: proposed source-backed cards for review before implementation. No production edit, build or execution is claimed by this document. These cards preserve supported agent and Process functionality; disabling ordinary execution is not the completion criterion.

## Actual execution classification

| Context | Current durable evidence | Current definition behavior | Required cutover |
| --- | --- | --- | --- |
| New chat turn with admitted governance and retained input | `AdmitInteractiveRun` creates a journal from the actual run/chat/authority; MAF obtains its run lease. | Protected source preparation, original intent, receipt and disclosure path is active. | Retain this implementation and its actual MAF restart proof. |
| Chat turn with request-scoped file input or unsupported transient attachment | A real admitted session exists, but `Support=RequestScopedInput`. | `UsesJournal` is false; the definition tools take their older path. | Preserve mutation functionality through an effect journal at the admitted run; do not label unavailable model input as restartable. Card R1. |
| Claimed Process agent step with recoverable input | `ProcessToolBackgroundSourcePolicy` verifies actual saved dispatch evidence; the same-source reservation stores the journal and background binding. | Protected Process definition path is active. | Retain this implementation and expired-dispatch/read-allowed replay proof. |
| Claimed Process step with request-scoped input | Original Process owner evidence exists, but journal support is request-scoped. | Same older-path gap as chat attachments. | Card R1 with Process current dispatch/source checks for first effect and independent current read for receipt recovery. |
| Detached ordinary agent execution, automatic or approval-driven | `ExecuteRunAsync` already saves a real `ExecutionRunRecord.Id`; it has neither a chat authority nor an installed Process background policy. | No tool journal. Purpose is AutoApprovedNonInteractive when auto-approve is true; otherwise `Unspecified` can map to InteractiveChat in capability composition. The purpose name is not proof of an admitted chat. | Reuse this owner-created execution ID and add a typed detached admission to the existing journal. Card R2. Do not synthesize a chat or Process claim. |
| Old saved chat/run without a journal | Existing historical run, with no saved business intent/source preparation. | Legacy paths can execute; a new durable identity cannot truthfully be inferred for an old dispatched effect. | Preserve old outcomes/read access. Mark old unresolved effects for explicit reconciliation; create a new owner-admitted semantic run only for new work. Never retroactively fabricate the old operation. |
| Process tool inventory/preflight | `ToolInventoryOnly=true`; no execution run, claim or lease yet. | Provider creates declarations for capability preflight; returned tools are not invoked. | Keep inventory available. An actual invocation without owner admission must fail; never allocate an operation during inventory. |
| A2A endpoint | The provider purpose exists in the enum. | Workbench's descriptor does **not** advertise A2AEndpoint, so these three definition tools are not an advertised A2A feature. | Preserve exclusion; do not claim a new unsupported A2A writer must be invented for this cutover. |
| Scenario harness/development execution | The harness calls ordinary `ExecuteRunAsync`; it can therefore be detached and is not inherently safe merely because its source says `scenario-harness`. | Falls into the same detached gap when tools are enabled. | Exercise R2 through real isolated fixture runs. Keep diagnostic-only fixture seeding separate. |
| HR manager review | Its source explicitly disables runtime tools, workspace tools and tool capabilities. | No definition tool producer. | No definition mutation change required. |
| Built-in Workflow LLM and Structure executors | LLM component is a stateless invocation with zero tool calls; Structure executor uses typed saved execution occurrences for reads, asset creation and task-node creation. | Neither currently exposes a project-definition create/update operation. Workflow-driven agents through Process still use the Process row above; workflow HTTP calls use the HTTP contract. | Preserve actual existing operations. Their asset/task concurrency belongs to the remaining-owner plan. Do not equate every Workflow occurrence with a new Projects writer. |

Source anchors: `AgentFrameworkWorkspaceExecutionService.Helpers.cs` (`AdmitInteractiveRun`); `BackgroundTools.cs` (`AdmitBackgroundRunAsync`); `ExecutionRuns.cs` (`CreatePreparingRun`, `ResolveRuntimeContextPurpose`, runtime options); `AgentToolAdmissionJournal.cs` (`RequireRecoverable`, `BeginSegmentAsync`, `AdmitBatchAsync`); `AgentToolAdmissionJournal.Background.cs`; `MafAgentExecutionAdapter.cs` (`supportsToolRecovery`, `canRecoverToolInvocations`); `RuntimeCapabilityComposer.RuntimeToolProviders.cs`; `RuntimeCapabilityComposer.cs` (`ResolveContextPolicyKind`); `ProjectStructureAgentRuntimeToolProvider.cs` (`Descriptor`, `CreateToolsAsync`); `ProcessRuntimeToolPreflightService.cs` (`CreateProviderContext`); `ProcessToolBackgroundSourcePolicy.cs`; `WorkflowLlmComponentInvoker.cs`; `ProjectStructureWorkflowExecutor.cs`; `HrAgentProcessReviewService.cs`; `ScenarioHarnessSupport.cs`.

## H1: protected Structure HTTP without changing caller permissions

Current routes are `GET /api/project-structure/projects/{id}/definition`, `POST /api/project-structure/projects`, and `PUT /api/project-structure/projects/{id}`. The existing `/subprojects` HTTP route links/reconnects projects; it is not the runtime tool's create-subproject operation. Do not change hierarchy operations under a definition-only card.

The group currently requires `WriteProjectStructure` for both reads and writes. `ProjectDefinitionHttpAccess` instead requires Projects read/write scopes. Reusing that helper unchanged would silently remove supported Structure-only clients. Share the verified caller resolution primitive, but make the Structure adapter explicitly require its existing current `WriteProjectStructure` scope for reading and dispatch. Current receipt access repeats that read policy and the same verified issuer/subject binding. Authorization-disabled mode explicitly selects OpenApi. The JWT `jti` still identifies a lease session, but must not replace issuer/subject as the durable caller key. `ResolveAgentContext` supplies lease/audit context, never the operation caller identity.

Smallest transport additions, reusing Projects contracts:

1. Return the existing coherent snapshot plus `ProjectDefinitionWriteContext` from the definition read, with the same before/after namespace/caller fence as ordinary Projects HTTP. Add a context-only read for root creation and a receipt lookup under the Structure route group. All use the Structure current read policy.
2. Use a transport-only typed request containing `ProjectDefinitionRequest<UpdateProjectDefinitionScalarsCommand>` plus an optional `LeaseToken` for scalar update. The token is current dispatch authority, not semantic content: a new valid lease can authorize the same frozen operation, while the original command/source cannot change. Do not put caller identity in JSON. Root create accepts `ProjectDefinitionRequest<CreateProjectDefinitionCommand>`; the legacy scalar-only create shape maps to explicit empty child collections only in a one-time client migration, not to a new operation generated on every server request.
3. Add a narrow Workbench protected scalar entry point taking the typed request, server-built `ProjectDefinitionInvocation`, existing `ProjectStructureAgentContext`, and lease token. Lookup exact accepted outcome first using current read access; acquire/revalidate the existing mutation lease only for a potential first effect; call protected writer; preserve accepted result across lease cleanup failure. A typed writer outcome stays typed through the adapter. No fresh target expectation or epoch is filled in.
4. Root creation can call the protected writer directly with the server HTTP invocation because this HTTP actor is an authenticated/open operator rather than a claimed catalog agent. Do not pretend `ProjectStructureAgentContext.AgentId` is a verified catalog Agent admission. Reuse the protected HTTP response mapper and exact accepted payload instead of reading a new summary after commit.
5. Enforce hard version-one body/field/count/aggregate bounds before request construction, and current limits only for new effects. Preserve Structure's documented serializer contract deliberately; add a binding test for its enum format. Log analytics after outcome selection, with failures unable to replace known acceptance. Do not send the protected union through generic `ExecuteAsync<T>` and accidentally wrap it as HTTP 200 or expose a generic error after commit.
6. Replace the legacy write routes together with their API client/tool documentation and integration tests. A missing operation envelope fails deterministically; the server does not create an implicit replay identity. Existing coherent reads and current authorization remain available.

Files: Web `ProjectStructureAgentApi.cs`, a focused new `ProjectStructureDefinitionApi.cs`, the verified caller seam in `ProjectDefinitionHttpAccess.cs` or its existing principal resolver, Workbench `ProjectStructureAgentService` protected adapter partial, and scoped route tests. No new receipt store, authority registry or dependency from Projects to Web/Workbench.

Required tests (names define the intended proof; they do not exist yet):

- `Structure_definition_HTTP_retains_structure_scope_and_reuses_verified_subject_across_token_renewal`.
- `Structure_definition_HTTP_first_effect_requires_current_lease_but_accepted_replay_requires_only_current_read`.
- `Structure_definition_HTTP_rejects_body_actor_route_mismatch_and_missing_operation_without_writing`.
- `Structure_scalar_operation_preserves_children_and_returns_exact_historical_ack_after_later_edit`.
- `Structure_definition_HTTP_current_scope_revocation_blocks_receipt_disclosure`.
- `Structure_definition_HTTP_accepted_result_survives_lease_and_analytics_cleanup_failure`.
- `Structure_definition_HTTP_retained_request_uses_issued_version_limits_and_never_refreshes_epoch`.

## R1: admitted request-scoped input retains effect recovery

The concrete persisted schema, detached original permission projection, atomic new-run validation, affected consumers, and implementation gates are refined in [the R1/R2 schema card](P02b-runtime-journal-schema.md). That card supersedes the preliminary shape discussion below where it is more specific; it is still a design proposal, not implementation proof.

This is not solved by widening `UsesJournal`. `AgentToolJournalRecord.Validate` forbids batches/segments when support is not Recoverable; `BeginSegmentAsync` and `AdmitBatchAsync` independently enforce that rule; MAF skips the protocol when support is RequestScopedInput. Current guards accurately report that full provider restart is unavailable.

The smallest supported behavior is a **versioned effect-only mode on the existing journal**, retaining the exact proposed tool arguments/source preparation and outcome without promising provider continuation from unavailable file bytes. Use a closed `AgentToolAdmissionSupport.EffectJournalOnly` value and a new journal schema version; retain the existing request-scoped format for historical runs. The owner selects the new mode for **new** admitted runs whose source/session is durable but whose original model input is not recoverable. Do not mutate old support values in place.

Keep the same run lease, business-intent ID, approval binding, owner receipt provider and SourcePreparation. Add a segment form that records the exact provider response/batch identity but makes the absence of a replayable provider-input checkpoint explicit. Its validation must prohibit automatic provider redispatch/continuation after restart. Model execution can still happen within the live original request; proposed definition mutations are durably admitted before tool execution. After response loss or restart, the journal can reconcile the saved owner effects and disclose exact receipts under current read authority. Continuing the model requires explicitly supplied/re-admitted input and a new semantic attempt after unresolved old effects are resolved. This preserves mutation functionality without silently retaining arbitrary file bytes or inventing recovery inputs.

Implementation order:

1. Models/Core: add the closed mode and schema-specific validation, an explicit effect-only segment constructor, and journal APIs for admitting/draining that exact segment. Maintain append-only intent/source immutability and real dispatch leases. `RequireRecoverable` stays on provider restoration; effect admission uses a narrower capability check.
2. MAF: split the current single boolean into durable effect admission versus provider-input recovery. Register the response-batch admission wrapper in both modes, but call provider restore only with an actual durable checkpoint. Approval decisions remain linked to exact persisted proposals; completion with unknown effects blocks a new batch.
3. Workbench: `ProjectDefinitionToolAdmission` may use a verified effect-only journal for these three tools. Its original Agent/Process source verification and frozen epoch remain identical. Generic result disclosure must use the same current read check. Do not broaden every other tool provider implicitly.
4. Execution owner: `AdmitInteractiveRun`/`AdmitBackgroundRunAsync` select the mode for new request-scoped runs. UI/API run state must state that effect reconciliation is available but model continuation needs fresh input; it must not present automatic restart as available.

Required tests:

- `Chat_with_request_scoped_attachment_durably_admits_definition_before_dispatch`.
- `Request_scoped_definition_response_loss_reconciles_exact_receipt_without_reopening_attachment_or_calling_provider`.
- `Request_scoped_pending_approval_keeps_original_source_and_requires_explicit_input_for_new_provider_attempt`.
- `Process_request_scoped_definition_first_effect_rechecks_claim_and_replay_survives_expiry_with_current_read`.
- `Effect_only_journal_cannot_restore_provider_checkpoint_or_mutate_saved_input_support`.
- `Effect_only_definition_recovery_does_not_reexecute_completed_intent_after_restart`.

Acceptance gate: actual MAF invocation with a real request-scoped attachment and actual persisted journal, not only manually built tool context tests. The source schema/segment shape must be reviewed before implementation; adding a permissive enum without these guards is not acceptable.

## R2: detached ordinary execution reuses its actual owner identity

`ExecuteRunAsync` creates and persists a run even without a chat. `AdmitBackgroundRunAsync` returns it unchanged when its source is not registered; only Process supplies the current background policy. `AgentToolSessionReference` currently requires either a nonempty chat+authority pair or a background source; no detached variant exists. This explains why a purpose-only patch cannot be correct.

Reuse `ExecutionRunRecord.Id` as the durable execution identity. Add a closed **detached owner admission** branch to the existing `AgentToolSessionReference`, with a server-created immutable authority binding captured when a new run is created. Proposed `AgentToolOwnedExecutionSourceBinding` carries the owner admission ID, agent ID, existing profile/generation binding and bounded authority projection reference/fingerprint; it must not carry a live lease, secrets, mutable CLR object or client-supplied source correlation as authority. The run ID remains the identity; the new binding records why that run is authorized, not a new deduplication system.

The Agent execution owner captures the existing catalog/project permission ceiling at creation, narrowed by trusted workspace intent. It persists that original projection with the run and verifies current source read/dispatch separately. New-run creation must be atomic with its initial journal and authority snapshot; an old run cannot gain this branch on restart. Do not create a hidden ChatSessionRecord or call a detached run a governed Process.

Smallest entry-point changes:

1. Extend the existing session model/validation and journal verification with exactly one of interactive, Process-background or detached-owner source. Add `CreateForNewOwnedExecutionAsync` alongside the existing constructors; use current execution-store mutation and lease interfaces. An owner source observer validates the saved run/profile/agent/projection and current policy. Core owns admission contracts; Modules.AgentFramework supplies application authority observation. Projects remains uninvolved in execution-store persistence.
2. In the non-chat branch of `ExecuteRunCoreAsync` in `ExecutionRuns.cs`, after `CreatePreparingRun` and before `PersistNewExecutionRunAsync`, create the detached admission when no registered Process source owns the run. Apply the same rule to `ExecuteSameSourceRunEntryAsync` before its existing same-source reservation. The admitted purpose preserves automatic versus approval-driven behavior; changing auto-approval does not create a new effect identity or widen permissions.
3. Pass the resulting existing `AdmittedToolSession` through runtime options; MAF reuses the current journal/lease dispatch and restoration machinery. Retain original saved source on restart. Do not derive identity from RuntimeSessionKey, `jti`, activity stream ID, source strings or provider call ID.
4. Workbench extends its Agent source codec/binding with an explicit detached encoding domain/version. Caller key binds verified run+agent+profile/owner admission; material effect binds the original permission ceiling, exact proposal, original expectation and source witness. Current receipt read validates the same owner-run read policy and current target permission independently of write. Closed Agent/Process effect kinds need not become a generic arbitrary source-kind string.
5. Update definition metadata selection to use the verified admitted branch, not `Purpose` alone. Keep automatic approval behavior for legitimate detached automatic runs; keep manual approval behavior for ordinary detached runs. Remove the three tool methods' legacy fallback only once these real launch paths pass their tests.

Required tests:

- `Detached_API_agent_execution_autoapprove_creates_one_protected_definition_and_replays_original_intent_after_restart`.
- `Detached_agent_execution_manual_approval_binds_exact_original_definition_source_and_operation`.
- `Detached_execution_current_write_revocation_prevents_first_effect_but_read_allowed_receipt_recovery_succeeds`.
- `Detached_execution_current_read_revocation_hides_historical_receipt_after_restart`.
- `Detached_execution_client_source_metadata_cannot_forge_Process_or_chat_authority`.
- `Legacy_detached_run_cannot_acquire_a_fabricated_original_operation_on_resume`.
- `Inventory_only_Process_provider_lists_definition_tools_without_creating_journal_or_operation`.
- `Scenario_harness_definition_mutation_uses_real_owner_run_admission`.

Acceptance gate: invoke actual `IAgentFrameworkWorkspaceService.ExecuteRunAsync` with and without auto-approval through the real provider/MAF seam. The existing execution ID must survive response/checkpoint loss. A manually constructed context with a purpose enum is insufficient. The detached authority projection is the only new owner capability to settle before implementation; no broad identity framework or replacement execution store is proposed.

## Closure order and boundaries

H1 is independent of R1/R2 and can ship after its typed contract is agreed. R1 and R2 are model/Core/MAF changes and require a serialized schema/compatibility review, then actual launch integration tests. Keep the already-supported recoverable chat/Process path working throughout. Only after both cards preserve current supported execution should the older runtime definition fallback be removed.

Workflow functionality remains included: actual Process-driven agent workflows keep their admitted Process lineage; ordinary HTTP workflows use H1/protected Projects envelopes; existing typed Structure asset/task workflow effects retain their own owner protocols and are covered by the wider P05/P07 plan. No unsupported project-definition Workflow executor is added merely to make the inventory appear complete.
