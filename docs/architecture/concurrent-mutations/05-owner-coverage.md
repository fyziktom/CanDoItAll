# Mutable owner coverage inventory

Status: read-only source review on 2026-10-16; no runtime tests or complete owner closure
are claimed. Baseline is `ead988abeb4cfb3e12341e618362bfa3dde5df29`. Projects definition
coverage is detailed separately in [editor design](03-editor-and-validation-design.md).
This inventory prevents the Projects pilot from being mistaken for engine-wide protection.

2026-10-19 policy recovery update: [historical Agent policy receipts](../../../codex/bundles/concurrent-mutations/validation/2026-10-19-agent-policy-receipts.md)
pass 40 focused cases. The dedicated file owner atomically stores exact original
acceptance, preserves it through ordinary writers/deletion, and supports read-only
HTTP lookup under current context and read permission. It does not lock unrelated
Agents for receipt-only changes. Native persisted recovery and wider owner closure remain open.

2026-10-19 update: the [external Agent checkpoint](../../../codex/bundles/concurrent-mutations/validation/2026-10-19-external-agent-preconditions.md)
passes 54 focused cases and replaces cached-binding preconditions with actual
target/revision/context checks. Delayed request capture, catalog-only publication,
historical replay and pinned postcommit projection are covered. Other Agent writers,
operation retention, restore and global profile-activation fences remain open.

2026-10-19 Process update: [immutable policy admission](../../../codex/bundles/concurrent-mutations/validation/2026-10-19-process-policy-admission.md)
passes 68 focused cases. Prepared launch and assignment persistence retain bounded
original policy evidence without rewriting legacy launches. Actual Agent/Workflow
adapters reject this contract pending the governed executor and continuation work.
This is storage/compatibility coverage, not an activated Process policy producer.

## Meaning of status

- **Enforced path observed**: source checks the caller's original expectation at the
  named write boundary, with a database predicate/guard or coordinated file write.
  This is code evidence for that path, not proof of every adapter or every writer.
- **Conditional/partial**: a supplied expectation or some operations are checked, but
  missing expectations, alternate writers or related mutations remain unclosed.
- **Gap observed**: a user-authored replacement loads current state and writes submitted
  values without an original content precondition in the inspected path.
- **Unknown**: there is not enough inspected evidence to classify the write guarantee.

A token column, `ApplicationManagedConcurrencyTokens.Stamp`, a UI busy flag, a current
row read, project admission, a runtime claim or a local semaphore alone does not prevent
a sequential stale editor from overwriting a prior commit. Preservation of the existing
domain/authority guards is required regardless of content-concurrency status.

## Owner inventory

Paths below are repository relative. Grouped owners need separate operation inventories
before their adoption wave; a row is not a claim that their entire module is closed.

| Mutable data and owner | Status and inspected behavior | Exact source entry points and next closure work |
|---|---|---|
| Workbench graph nodes, links, metadata and placement | **Enforced paths observed / perimeter partial.** Original node/link/graph expectations and project admissions exist; collection witnesses protect some branch operations. Preserve value/state semantics rather than replace them with one Projects token. | `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectAgentNativeMutationService.cs`, `ProjectStructureAgentContracts.cs`, `ProjectStructureEditConflictException.cs`; source/HTTP cases in `tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureStaleEditHttpTests.cs`. Close direct UI, Gantt, tool, process, workflow, import, move and maintenance writers independently. |
| Canonical task details, schedule, assignment, pricing and execution inputs | **Enforced paths observed / perimeter partial.** Gantt checks expected title/progress/estimate/execution/cost/assignment/schedule; task owner has original state contracts. | `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureGanttMutationService.cs`, `ProjectStructureTaskDetailsService.cs`, `ProjectStructureTaskPricingCommitService.cs`, `ProjectStructureWorkItemAssigneeService.cs`. Do not introduce a broad token that invalidates unrelated progress or erases pricing/assignment invariants. |
| Workbench planning outputs and publication basis | **Unknown/incomplete for new requirement.** Existing source authority is not a material-decision-basis policy. | `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAgentService.cs`, `ProjectAgentSourceMutationAuthority.cs`, `ProjectProcessExecutionMutationService.cs`. Add the trusted bounded basis/attempt policy described in `02-agent-process-policies.md`; recheck set membership where planning depends on a query. |
| CRM opportunity edit and conversion | **Enforced paths observed.** Existing edits require `ExpectedUpdatedAtUtc`, compare it and map EF conflict; conversion also binds EF original value. | `src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrServices.cs`: `CrmService.SaveOpportunityAsync` and conversion path. Retain current source/lifetime and cross-owner creation compensation. Audit other opportunity mutations and actual UI/API/tool serialization before closure. |
| CRM affiliations | **Enforced paths observed / perimeter partial.** Batch replacement rejects existing rows without expected timestamps and mismatches; upsert forwards the original timestamp. | `src/Modules/CanDoItAll.Modules.CrmHr/Services/PartyOrganizationAffiliationService.cs`, `CrmPartyCommandService.cs`. Audit deletion, directory merge/import and direct relationship writers that can alter affiliation state; protect set membership for replacement. |
| CRM project party assignments | **Enforced replacement path observed / perimeter partial.** Replacement compares supplied assignment snapshots under owner scopes; project admission and pricing revisions exist. | `src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrServices.cs`: `ProjectPartyIntegrationService` replacement, `SaveAssignmentAsync`, deletion/move methods; `ProjectPartyAssignmentInvariantPolicy.cs`. Single-row saves, bulk moves and compensation need their own original-condition audit. |
| CRM account details, directory relationships, HR workforce profiles, skills, capacity, staffing and AI business profile | **Gaps observed / remaining operations unknown.** `SaveAccountProfileAsync` and `SaveWorkforceProfileAsync` load current records then overwrite editable values without a submitted content condition. Related editable methods cannot inherit the opportunity guarantee. | `src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrServices.cs`, `PartyDirectoryManagementService.cs` (`SaveRelationshipsAsync`, merges/imports). Separate personal data, rates, workforce/capacity predicates and technical-agent projection ownership. Inspect every update/delete and collection replacement, not only row tokens. |
| Technical agent definitions | **Conditional/partial.** `ExpectedUpdatedAtUtc` mismatch is checked when supplied; the core method allows its absence. | `src/MAF/Common/CanDoItAll.AgentFramework.Core/Catalog/AgentFrameworkWorkspaceCatalogService.Agents.cs`; native verification in `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentEditorVerification.cs`. Close normal UI, HR provisioning, recruiting, tool and external provisioning writers; distinguish configuration revision from live execution claim. |
| Agent teams and capability definitions/assignment | **Conditional/partial or unknown.** Capability editor fingerprint is compared only when nonempty. Team write perimeter is not proven by agent version checks. | `src/MAF/Common/CanDoItAll.AgentFramework.Core/Catalog/AgentFrameworkWorkspaceCatalogService.ProvidersAndCapabilities.cs`, `.Teams.cs`, `CapabilityEditorConcurrency.cs`; `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentCapabilityAssignmentAttempt.cs`. Missing fingerprint and all alternate writers require explicit admission; approval/capability authority remains independently current. |
| Provider profiles and runtime defaults | **Conditional/partial.** Database registry checks `ExpectedConcurrencyToken` only when supplied and translates EF conflicts. That protects participating callers, not tokenless replacements. | `src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/Administration/DatabaseProviderProfileRegistry.cs`, `ProviderAdministrationService.cs`, `ProviderRuntimeAdministrationService.cs`; `src/Modules/CanDoItAll.Modules.AgentFramework/Services/ProviderEditorOperations.cs`. Audit runtime probe/default/bootstrap and legacy catalog calls; background health must not overwrite a stale full configuration. |
| Shared provider source/import/publication configuration | **Enforced paths observed / perimeter partial.** Source update/enable/delete methods require nonempty tokens and compare them; import management compares import/provider tokens. Some sync/connection paths have distinct optional expectations. | `src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderSourceService.cs`, `SharedProviderManagementService.cs`, `SharedProviderPublicationApplicationService.cs`, `SharedProviderSourceSyncService.cs`. Preserve current authority and prove background sync cannot revert human configuration. |
| Simple Chat definitions and mutable conversation metadata | **Enforced paths observed.** Definition replacement SQL predicates on original concurrency token and current revision; conversation replacement predicates on its expected token. | `src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence/Repositories/EfLlmChatDefinitionRepository.cs`, `EfLlmChatConversationRepository.cs`; application `LlmChatDefinitionApplicationService.cs` and `LlmChatConversationApplicationService.cs`. Verify all adapters preserve those values. Transcript append/dispatch identity is separate from definition editing. |
| Workflow definition authoring | **Conditional/partial.** Persistent head compares `ExpectedVersionId` when supplied and handles EF conflicts; null expectations remain an enabled contract to inspect. Immutable execution versions already exist. | `src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs`; `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Core/WorkflowCatalogServices.cs`; native `Pages/Authoring/WorkflowDocumentOwner.cs`. Close curator/API/import/legacy callers and distinguish definition pinning from publication into current targets. |
| Process authoring definitions, roles, steps and project overrides | **Enforced path observed.** `CommitAsync` validates original head/inherited revision under ordered locks, binds caller/operation/fingerprint and writes an accepted/conflict receipt atomically with publication. | `src/Processes/CanDoItAll.Processes.Persistence/EfProcessAuthoringStore.cs`; `src/Processes/CanDoItAll.Processes.Application/ProcessAuthoringStore.cs` and authoring adapters. Reuse these existing semantics rather than add a parallel generic receipt protocol. Audit runtime imports/legacy definition sources separately. |
| Scheduler plan definition, enabled flag and deletion | **Gap observed.** `SavePlanAsync` reads the current plan then assigns submitted values; enable/delete take identity without original edit expectation. Target version and source admission protect a different fact. | `src/Modules/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs`, `AgentTools/SchedulerAgentRuntimeToolProvider.cs`, `Pages/SchedulerWorkspaceSession.cs`. Add plan-edit conditions without treating fire-history/projection/claim progress as user content. Preserve known-commit projection warning behavior. |
| Prompt Gallery draft and immutable version creation | **Enforced paths observed / remaining operations partial.** Existing draft ID requires expected timestamp; save compares it and handles concurrency; version creation requires original timestamp. | `src/Modules/CanDoItAll.Modules.Prompts/PromptsService.Persistence.cs`, `PromptsService.Gallery.cs`, `PromptGalleryMutationService.cs`, `PromptEntityConfigurations.cs`. Audit archive, suppression, collection membership, asset linkage and curator adapters separately; immutable versions are not mutable drafts. |
| Collaboration mutable thread/inbox state and message submission | **Partial/unknown.** Thread/inbox rows have EF tokens, but `AppendMessageAsync` and `MarkThreadAsReadAsync` load current rows without caller content conditions; appended message is coupled to preview/unread state. | `src/Modules/CanDoItAll.Modules.Collaboration/CollaborationService.Commands.cs`, `CollaborationModels.cs`. Define idempotent read/unread/status intents separately from append dedupe. Do not protect the whole transcript with one editing revision. |
| Resource registry metadata | **Gap observed.** `ResourcesService.SaveAsync` validates captured project admission and binding under a serializable scope, then replaces editable resource fields without original resource-content expectation. | `src/Modules/CanDoItAll.Modules.Resources/ResourceModels.cs`, `ResourceRegistryOwner.cs`. Retain project lifetime and file promotion authority; add resource metadata revision separately from external bytes. |
| TestLab plan definition | **Gap observed in inspected save.** Project admission protects project lifetime, not the editor's original plan content. | `src/Modules/CanDoItAll.Modules.TestLab/TestLabModels.cs`: `SaveAsync`; `Workspace/TestLabWorkspaceOwner.cs`. Treat user-authored plan edits as product data. Execution results remain history; fixture/scenario setup must be explicitly maintenance-only. |
| Workspace business defaults | **Gap observed.** `SaveSettingsAsync` selects the latest settings row then replaces submitted fields; draft copying/committed warning handling does not establish original concurrency. | `src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs`, `Presentation/WorkspaceDefaultsOwner.cs`. Use a settings scope separate from provider history, storage and Data Sources. |
| Storage catalog/routing configuration | **Gap observed.** `SaveCoreAsync` and routing save load current state then assign submitted values; editor-create/missing/protected checks are identity checks. Health writeback can call the same broad save path. | `src/Foundation/CanDoItAll.Infrastructure/Storage/Persistence/StorageCatalogService.cs`. Separate human configuration from driver health observations and verify routing-set predicates. Staged UI receipts do not supply stale-edit protection. |
| Data Sources/database profile catalog and activation | **Unknown original-content coverage.** Control-plane serialization, schema compatibility and active profile fencing are distinct from original edit conditions. | `src/Foundation/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs`; `src/Modules/CanDoItAll.Modules.Workspace/Database/DatabaseProfileWorkspaceService.cs`. Audit save/delete, connection test writeback, import and activation independently. Do not move control-plane files into a generic PostgreSQL mutation service. |
| Secrets metadata and rotation | **Gap observed.** Secret save copies the submitted draft, reads current metadata, writes a new vault key and updates metadata without original expected content. Explicit unknown/committed-warning handling exists. | `src/Modules/CanDoItAll.Modules.Security/SecurityModels.cs`, `SecretVaults.cs`, `NativeSecretVaults.cs`. Separate metadata CAS from provider-specific vault effect/cleanup; conflict/recovery must never return secret values. |
| API accounts/password/scopes | **Enforced path observed.** User administration forwards expected version; `FileApiUserStore` reacquires coordinated file state, compares expected version for update/delete and validates the new version before durable write. | `src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiUserAdministrationService.cs`; `src/Foundation/CanDoItAll.Infrastructure/ControlPlane/FileApiUserStore.cs`. Preserve authentication revision invalidation and control-plane ownership; test actual coordinated file semantics on supported platforms. |
| API token issue/revoke/delete | **Separate intent/unknown perimeter.** Commands are lifecycle operations rather than full editable record replacement. | `src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiTokenAdministrationService.cs`. Prove idempotent revoke, current authorization and outcome recovery; do not require a blanket editable-row token merely because the record changes. |
| Plugin grants, connections, installation settings and OAuth | **Gaps observed / OAuth unknown.** Grant `SetAsync` and connection `SaveAsync` load current rows then replace fields without submitted expected tokens despite token-bearing result models. | `src/Modules/CanDoItAll.Modules.Plugins/Catalog/PluginPermissionServices.cs`, `PluginCatalogServices.cs`, `OAuth/PluginOAuthService.cs`. Preserve per-effect grant evaluation and OAuth state security. Audit callback/refresh against human disconnect and config edits; install/restart/delivery are external lifecycle effects. |
| Memory provider configuration | **Gap observed.** `MemoryProviderProfileConfigurationService.SaveAsync` reads latest profile and applies submitted config; `EfMemoryProviderProfileStore.UpsertAsync` has no original precondition. | `src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderProfileConfigurationService.cs`; `src/Memory/CanDoItAll.Memory.Persistence/EfMemoryProviderProfileStore.cs`. Keep provider-neutral profile settings separate from ingestion/query/retention effects and disabled-by-default runtime policy. |
| Provider-history capture/retention settings | **Enforced path observed.** `HistoryPolicyStore.UpdateCoreAsync` locks policy row, compares `ExpectedVersion`, advances it, writes audit, rechecks access and commits. | `src/MAF/ProviderHistory/CanDoItAll.AgentFramework.ProviderHistory.Persistence/HistoryPolicyStore.cs`. Preserve profile-generation fence and shorter-retention semantics. History entries themselves are append/retention state, not editor snapshots. |

## Exclusions and remaining discovery

Append-only activity, audit, transcript messages, immutable prompt/workflow/process
versions, provider request history, execution events and result history do not need a
universal editor token. Their append identity, ordered sequence, retention, claim/lease
and recovery rules still require proof where changed. Mutable metadata attached to those
records, such as a conversation title or policy, remains in scope.

Storage/file bytes and external plugin/provider writes need the actual driver's
conditional-write/idempotency/status protocol; an EF row token cannot protect external
bytes. `src/Foundation/CanDoItAll.Infrastructure/Storage/Abstractions/StorageRevisionedContentContracts.cs`
and `Storage/Drivers/FileSystemStorageDriver.cs` are the next exact source seam; supported
driver capability coverage remains unclosed by this inventory.

Factory, custom process definition sources, workspace connectors, TestLab runner inputs,
external integrations and persisted personal layout/preferences need a final route/tool
and owner-store sweep before engine-wide completion. Folder names or absence of a grep
match are not proof that a surface is read-only. Classify personal presentation state
separately from collaborative business data; do not introduce global conflicts for local
selection or tab state. Imported data and recovery/maintenance commands are writers and
must remain in each owner's perimeter even when excluded from ordinary UI editing.

## Review conclusions and safe first implementation

The overall architecture and editor design agree on no-force explicit reconciliation,
typed original preconditions, independent authority, owner boundaries and measured costs.
The following items remain design gates for dependent slices:

1. Durable accepted results must contain enough exact normalized fields and child identity
   mapping for delayed UI acknowledgement. "Minimal outcome" cannot mean only an ID/new
   token followed by a read of a newer state. Choose bounded exact payload or an immutable
   owner result reference; never solve this by storing entire LLM contexts.
2. A dataset/caller/operation uniqueness tuple must state the trusted caller identity for
   native local UI and API, and its durability across restart; the opaque operation ID alone
   is not disclosure authorization. Restore/import fence and retention need real owners.
3. Missing preconditions must fail closed across all participating adapters, especially
   the existing SPA and optional agent/provider/workflow expectations. UI-only conversion
   leaves an enabled bypass.
4. Different owner guarantees must remain explicit. The user requested end-to-end engine
   behavior; completing Projects and a cleanup repair alone cannot close that request.
5. Performance wording must permit database network IO while excluding external provider,
   filesystem, human and model waits. Account for one immutable base plus live/submitted
   state; no unbounded history. A 100-operation preliminary sample can flag regressions,
   but its p99 is a coarse tail observation, not a precise production tail estimate.

The P01 `SerializableMutationScope` cleanup repair is a suitable independent first slice:
both acquisition failure and normal disposal currently await transaction disposal before
releasing owned process locks. A disposal exception can strand a stripe. Release must run
in `finally`, preserving both primary and cleanup failures where applicable. Prove failed
begin, normal/throwing disposal, repeated disposal and partial-acquisition cancellation,
plus a blocked successor that eventually acquires the released scope. No new reference,
owner protocol or lock ordering is needed. It is a liveness/cleanup repair, not proof of
stale-edit safety, and does not require settling later merge or wire migration details.
