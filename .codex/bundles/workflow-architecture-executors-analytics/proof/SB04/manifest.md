# SB04 Proof Manifest

- Subbundle ID: SB04
- Status: Complete; combined bundle closure remains in SB06/SB07
- Owned runtime requirements: incremental lifecycle, active cancellation, honest external-response resume, atomic persistence, governed agent tools, and explicit process-assignment workflow execution
- Semantic invariant contract: bundle://proof/SB04/semantic-invariants.md

## Runtime Lifecycle Evidence

- Failing-first transcript: bundle://proof/SB04/transcripts/closure.txt
- Passing transcript: bundle://proof/SB04/transcripts/closure.txt
- Anti-stub transcript: bundle://proof/SB04/transcripts/closure.txt
- Failing-first: bundle://proof/SB04/failing-lifecycle.txt
- Passing lifecycle: bundle://proof/SB04/passing-lifecycle.txt
- Passing PostgreSQL persistence: bundle://proof/SB04/passing-persistence.txt
- Passing build: bundle://proof/SB04/passing-build.txt
- Architecture snapshot: bundle://proof/SB04/architecture-snapshot.txt

## Caller Migration Evidence

- Failing-first: bundle://proof/SB04/failing-callers.txt
- Passing focused callers: bundle://proof/SB04/passing-callers.txt
- Production callers covered: HTTP API, workflow test runner, scheduler plan runs, and project-structure workflow nodes

## Generic Agent Tool Evidence

- Passing generic workflow tools and MAF lineage composition: bundle://proof/SB04/passing-agent-tools.txt
- Registered tools: list active definitions, start production run, inspect run, request cancellation, and submit external response
- Mutation approval is governed centrally by `ToolCapabilityRegistry`; start, cancel, and response are wrapped unless the host explicitly suppresses approvals, while list and status remain reads
- Start delegates only to `IWorkflowLaunchService`, selects exact saved version or latest active within one workflow, waits for stopped/waiting, and carries the real agent, runtime session, correlation, purpose, and caller idempotency
- Runtime control delegates to typed manager outcomes and preserves unsupported resume honestly

## Process Workflow Execution Evidence

- Failing-first: bundle://proof/SB04/failing-process-workflow.txt
- Passing process driver, resolver, persistence, migration, adapter, and role-editor proof: bundle://proof/SB04/passing-process-workflow.txt
- Typed selection is one explicit workflow plus optional exact version; latest-active resolution never crosses workflow identity
- Retry/recovery trusts only persisted runs with matching typed process-run and assignment origins, and the launch result is independently revalidated
- The process role editor preserves the typed binding across edits and exposes validated BaseLib fields for latest-active or exact-version selection

## Named Test Proof

- Test name: StartPersistsRunningAndStartedBeforeBackendRelease
- Test name: InitialPersistenceFailurePreventsBackendInvocation
- Test name: StartedEventPersistenceFailurePreventsBackendInvocation
- Test name: BackendFailurePreservesProgressAndPersistsFailedRun
- Test name: ActiveCancellationSignalsBackendToken
- Test name: LateBackendCompletionCannotOverwriteCancelled
- Test name: CallerCancellationWinsRaceWhenBackendIgnoresTokenAndDisablesOutOfBandCancellation
- Test name: NonActiveCancellationDoesNotFabricateCancelledState
- Test name: InProcessExternalResponseRemainsWaitingWhenResumeUnsupported
- Test name: ResumeCapableBackendAcceptsExternalResponseExactlyOnce
- Test name: PersistentStoreEnforcesAtomicLifecycleAndExactlyOnceExternalResponse
- Test name: ProviderExposesFiveGovernedToolsWithAuthoritativeMetadata
- Test name: RuntimeComposerWrapsOnlyWorkflowMutationsUnlessHostSuppressesApproval
- Test name: ListToolReturnsLatestActiveVersionEvenWhenLatestCatalogItemIsDraft
- Test name: StartToolUsesGovernedAgentOriginAndExplicitVersionSelection
- Test name: StatusCancellationAndResponseToolsPreserveTypedRuntimeOutcomes
- Test name: MafAgentRuntimeToolProviderComposition_propagates_authoritative_runtime_session_key
- Test name: ResolveAsync_binds_only_the_explicit_workflow_and_latest_active_version
- Test name: ResolveAsync_rejects_exact_workflow_version_that_is_not_active_and_runnable
- Test name: ResolveAsync_rejects_workflow_kind_without_explicit_workflow_instead_of_selecting_any_active
- Test name: ExecuteAsync_launches_exact_workflow_with_typed_process_origin_context_and_idempotency
- Test name: ExecuteAsync_recovers_completed_typed_origin_child_without_duplicate_launch
- Test name: ExecuteAsync_rejects_launch_result_that_does_not_match_typed_assignment_identity
- Test name: ExecuteAsync_delegates_typed_workflow_assignment_before_agent_and_subprocess_paths
- Test name: Runtime_step_assignment_store_round_trips_launch_variables_for_execution_metadata
- Test name: Workflow_role_binding_round_trips_and_is_not_erased_by_later_role_edits
- Test name: Workflow_role_binding_rejects_invalid_guid_before_save

## Runtime Slice Result

- Running and Started are committed atomically before backend invocation.
- Incremental safe progress survives backend failure and carries usage metadata without raw payload duplication.
- Out-of-band cancellation is capability-gated; caller cancellation remains linked; completion/cancellation races resolve through one active-run lease and conditional terminal persistence.
- External response is accepted exactly once only when a backend advertises and implements resume; in-process unsupported resume leaves the run and request untouched.
- In-memory and PostgreSQL stores implement explicit atomic creation, conditional transition, and conditional response primitives.
- Process assignments now resolve, persist, launch, recover, wait, and map workflow children through one typed adapter; the unused direct-start bridge is removed.
- Process workflow identity and output mapping are migration-backed, and the EF model reports no pending changes.

SB04 is complete. SB07 still owns the combined solution/browser closure across all concurrently implemented subbundles.

## Changed-File SHA-256

| File | SHA-256 |
|---|---|
| `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowRuntimeManager.cs` | `83951355b5bb50d3bb1dc94fbe2b767183dc33cc5da06c1688c2c7e41d0e9dff` |
| `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Core/WorkflowLaunchService.cs` | `a360c2f208229f04688a044dbc9ebfca538e8a33ba37180859c6c8315c67a654` |

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Workflow run lifecycle and Started/terminal events | `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowRuntimeManager.cs` | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs` | `bundle://proof/SB04/passing-lifecycle.txt` | `bundle://proof/SB04/failing-lifecycle.txt` and `bundle://proof/SB04/semantic-invariants.md` |
| Launch idempotency claim and reserved run identity | `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Core/WorkflowLaunchService.cs` | `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowRuntimeManager.cs` | `bundle://proof/SB04/workflow-launch-idempotency.md` | concurrent claim, conflict, lease-takeover, and post-persistence recovery proof in `bundle://proof/SB04/workflow-launch-idempotency.md` |
