# P04/P07: governed decision data and runtime policy

Status: implementation plan, source-inspected 2026-10-16. No production changes,
build, test discovery, test execution or performance result is claimed here. This
refines architecture 02 and the owner inventory 05; it does not close either phase.
Paths are repository-relative. The archived architect documents remain design input.

## Outcome and scope

An Agent, Process step or Workflow node can deliberately keep its starting business
facts or read current facts at the next logical step. Its publication must still obey
the effect owner's currentness mandate, target preconditions and current authority.
Changed material facts cause a typed no-effect result followed by the configured
durable pause, terminal failure or bounded semantic replan. Restart, transport retries,
executor retries, nested execution and approval waits must not silently change that
contract or reset its budgets.

The first P04 operation is a bounded task-plan proposal and atomic publication owned
by Workbench. P07 applies the same typed policy and lifecycle to real Agent, Process
and Workflow entry points. This is a proving operation, not an engine-wide completion
claim. CRM planning, assignments/rates/capacity, resources, external files, scheduler
and all mutable owners in architecture 05 retain their own closure work. The first
operation must explicitly reject an unsupported material source rather than label it
current based on a preflight read.

## Decisions to freeze before coding

| Contract | Initial supported semantics | Invalid substitution |
| --- | --- | --- |
| `ReadBasisMode.RunSnapshot` | At run admission capture a declared bounded owner scope before dependent model/provider work. Reuse that immutable basis for subsequent enrolled steps. | A claim to freeze the entire workspace, an ambient dependency collector, or a later unannounced extension of the admitted scope. |
| `ReadBasisMode.StepSnapshot` | Capture once immediately before admitting a logical decision step/turn. Reuse it for every replay/recovery/executor retry of that logical attempt. The next logical step captures again. | Re-reading between tool retries, or calling a new GUID a new step to evade a conflict. |
| `PublicationFreshness.RequireCurrent` | Owner validates every mandatory material witness under the same transaction/fence that publishes its effects. | Current permission, project lifetime, current definition version, a timestamp or a prior read standing in for material currentness. |
| `PublicationFreshness.PinnedObservation` | Only an owner operation that explicitly publishes an historical observation may support it; result identifies its basis. Current authority and original target expectation remain required. | Force-write, stale task-plan publication, or weakening a server-selected minimum. |
| `OnBasisChange.PauseForReview` | Persist a no-effect changed-basis proposal and reason, release all resources, await an explicit decision through the actual host's durable continuation contract. | Returning a successful pause before persistence, holding an HTTP call/claim/transaction, or treating a checkpoint display row as a resumable backend. |
| `OnBasisChange.Fail` | Persist/report a terminal no-effect outcome. Stateless paths support only this response. | Falling back from a requested pause/replan without telling the caller. |
| `OnBasisChange.Replan` | Explicitly authorized planning-only response: spend a durable semantic-replan budget, admit a new basis and intent, regenerate, obtain a new approval where required. | Retrying an external effect, editing an approved payload, reusing an old approval or resetting a budget. |

Keep the existing names `RunSnapshot` and `StepSnapshot`. “Pinned snapshot” and
“latest before step” describe them, not extra modes. Remove the proposed
`RefreshBeforeProposal` from the initial vocabulary: it either duplicates
`StepSnapshot` or requires a semantic replan. Introduce it later only if a distinct,
server-recognized boundary and useful behavior are demonstrated and tested.

Default for a supported durable planning host: `StepSnapshot`, `RequireCurrent`,
`PauseForReview`, zero automatic replans. A stateless command explicitly supports
`Fail` only; requesting pause/replan is rejected before work. Do not temporarily
enable the durable default on a backend whose pause card is incomplete. Invalid enum
values, incompatible combinations, missing original basis and missing owner support
are visible validation errors. No silent downgrade or invented provenance.

Resolve workspace preference, definition preference, step/node override, then authorized
invocation override. A typed owner capability/mandate constrains the result at every
admission. Client preferences can choose only supported behavior and never relax the
owner minimum. Budgets are bounded by every enclosing policy. Persist the resolved
policy, protocol version and relevant policy-source revisions. New settings affect new
admissions; retries and resumes use the original resolved policy. Current revocation
or newly restrictive owner mandates still block effects and disclosure.

Use minimal data-only vocabulary in `CanDoItAll.SharedKernel`: the two read modes,
publication requirement, change response, a validated immutable resolved policy and
a nullable-field override record. Include bounded replan count, work duration and
basis age; only offer a spend ceiling when the runtime can account for/enforce it.
A pure resolver can live beside these records. It accepts values and capabilities;
it does not query settings, resolve services, access EF, know Projects or own approvals.
The owner-specific basis and task-plan commands stay with Workbench. Do not create a
universal transaction coordinator, runtime plugin registry or another execution partial.

## Actual integration seams and storage

These are source observations, not claims that data-policy fields already exist.

| Owner | Current seam and consequence | Minimal policy integration |
| --- | --- | --- |
| Workspace settings | `src/Modules/CanDoItAll.Modules.Workspace.Contracts/WorkspaceSettingsModel.cs:6` and `:26` model/copy; `Models/WorkspaceModels.cs:13`, `:85` entity/save; `Presentation/WorkspaceDefaultsOwner.cs:19` native outcome owner. Save currently loads/replaces latest content. | Add typed default override to model/entity/copy, API and native defaults editor. First close original settings revision across every settings writer (05); do not bolt an unprotected policy onto the current unconditional save. Runtime obtains defaults through a narrow owner-owned read port at admission, not a dependency on the Workspace module. |
| Agent definition | `src/MAF/Common/CanDoItAll.AgentFramework.Models/Editors/EditorModels.cs:12`, `:27`, `:111` editor/original timestamp/configuration; `Core/Agents/AgentDefinitionFactory.cs:7` normalizes typed configuration metadata; `Core/Catalog/AgentFrameworkWorkspaceCatalogService.Agents.cs:34`, `:52` save with optional expected timestamp. | Add typed `DataPolicy` editor property and owner metadata codec following existing typed configuration conventions. Copy/compare it in `src/UI/CanDoItAll.AgentFramework.Editor.UI/AgentEditorDraftSnapshot.cs`, module `Services/AgentEditorDraftPolicy.cs` and `AgentEditorCommands.cs`; round-trip `src/App/CanDoItAll.Web/Api/AgentsApi.cs:560`. Close missing expectation in ordinary existing-Agent saves first; identity/lifetime and timestamp assumptions remain the Agent owner's concern. |
| Process authoring | `src/Processes/CanDoItAll.Processes.Templates/ProcessTemplatePackLoader.cs:1174`, `:1256` definition/step documents; `ProcessTemplateDocuments.cs` source-generated JSON; `ProcessTemplateHashing.cs`; Application `ProcessTemplateKernelBuilder.cs`; authoring commits in `EfProcessAuthoringStore`. | Typed definition default and step override, included in canonical serialization/hash, immutable publication and compilation. Carry through `Processes.Projections/ProcessDefinitionStepEditorProjectionContracts.cs`, Application `ProcessDefinitionStepEditorProjectionService.cs`, UI `ProcessStepEditorState.cs` and `ProcessDefinitionStepEditorPanel.razor`, and existing native authoring commands. HTTP `ProcessDefinitionsApi.cs:27` is read-only and should expose the value; there is no existing HTTP definition-write route to assume or bypass. Do not encode policy in launch-variable strings or execution-class strings. |
| Process launch/assignment | `Application/ProcessLaunchApplicationService.cs:554` creates assignments; `Runtime/ProcessRuntimeStepAssignments.cs:6`; `Application/ProcessPreparedLaunchContracts.cs:43`; Persistence `ProcessPreparedLaunchPersistence.cs:68` serializes/hashes immutable preparation; `EfProcessRuntimeStepAssignmentStore.cs:205`, `:252` persistence and repair restrictions. | Resolve policy during prepared launch and include it in preparation fingerprint and immutable assignment evidence. Add explicit persisted field/JSON version and immutable comparison; repair may not change effective policy. Assignment holds policy and bounded basis reference, not a dashboard. Capture StepSnapshot business facts when the step is actually admitted, not eagerly for every future assignment during launch. |
| Process Agent dispatch | Module `Services/RuntimeIntegration/AgentFrameworkProcessStepExecutor.cs:336` constructs request; `ProcessAgentExecutionAdmission.cs:9` derives same-source identity from run+step and reuses/reconciles existing execution. | Carry resolved policy and decision lineage through a typed request field. Keep exact-source replay stable. A semantic replan requires an explicit persisted decision-attempt identity recognized by admission; merely calling this existing method again reuses the old source execution. Do not change run/step identity or lose its claim fence to manufacture a fresh run. |
| Workflow definitions | `src/MAF/Common/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs:694`, `:1009` node settings/definition; `WorkflowExecutorModels.cs:542` executor retry policy. Definitions validator and builder own typed validation/construction. | Add definition default and node override as typed siblings of executor `ExecutionPolicy`, not fields in executor-specific JSON. Update builder, import/export, save request, catalog and native `Pages/Authoring/WorkflowDocumentOwner.cs`. Close optional `ExpectedVersionId` ordinary updates in `PersistentWorkflowStores.cs` (05). Execution pins the immutable version; data refresh never swaps definition versions. |
| Workflow admission | Workflows.Core `WorkflowLaunchService.cs`, `WorkflowLaunchIdempotencyRequestFactory.cs:46`; Models `WorkflowLaunchModels.cs`; module `PersistentWorkflowLaunchIdempotencyStore.cs`. | Bind the resolved admission policy into launch fingerprint and run evidence. A repeated caller key with different policy conflicts. Nested Process/Agent launches carry the enclosing policy ceiling. Definition settings/hash and invocation occurrence must be included in the decision identity. |
| Workflow provider/disclosure | Workflows.Runtime `WorkflowProviderInputAdmission.cs:16` validates actual invocation, retained declaration, read completion provenance and owner disclosure; MafAdapter `MafWorkflowTopologyFingerprintFactory.cs:98` manually hashes node settings. | Add typed policy to fingerprint and original declaration as appropriate. Existing protected-read evidence is authority/provenance, not a general mutable-data witness. Preserve it alongside material-basis validation. Capture StepSnapshot once for the actual node occurrence; executor retry must not advance the occurrence or reread the basis. |
| Agent tool admission | Core `ToolPolicy/AgentToolAdmissionContracts.cs:6` proposal preparer; Models `Execution/AgentToolAdmissionModels.cs:131` prepared payload; `AgentToolJournalModels.cs`; Core `AgentToolAdmissionJournal`. | Dedicated Workbench planning `IAgentRuntimeToolProvider` with typed semantic codec. Bind owner basis reference/hash, policy and proposal into the existing digest/intent. `SourcePreparation` has an existing 65,536-byte limit: keep a bounded reference/envelope, not LLM context. |

Project dependencies must remain inward: product Workbench/Processes integration
adapters may depend on the neutral runtime and owner contracts; Processes.Runtime
must not reference MAF, product modules, EF or Workflow implementation; Agent.Core
must not reference Workbench/Projects. Existing Workflow models live in
AgentFramework.Models, not a new imagined Workflow.Models project. Existing SharedKernel
references make neutral data sharing possible; verify the evaluated graph before adding
any reference. Composition belongs in the owning registration/composition root.

The reused CodeAnalytics snapshot `snap-20261016142521-bf7089bf` covers four scoped
projects/361 documents and reports 187 diagnostics. It supports positive symbol
orientation, not absence-of-dependency or cycle proof. Direct project files confirm
the above boundaries. A healthy evaluated graph for changed projects is a pre-code
gate for new references and a post-integration gate; no healthy graph is claimed here.

## Material basis, membership and the atomic publication boundary

Workbench owns `ProjectTaskPlanBasis` and its versioned capture descriptor. For the
initial operation, trusted code selects the project objective/definition and the complete
canonical task membership/content necessary to avoid duplicate or contradictory plans.
The proposal cannot omit a dependency because the model did not mention it. Bind dataset
namespace, project lifetime, bounded owner facts, scope descriptor/version, capture time,
resolved policy, lineage and canonical hash. Store the exact normalized captured facts
needed for review/reconstruction separately from the small journal reference. Hashes
alone cannot reconstruct a pinned input after it changes.

Existing `Workbench/ProjectStructureGraphState.cs:19`, `:51`, `:61` captures editable
node content/parent/link originals and checks a selected graph under a Workbench context.
It is useful evidence of graph preconditions, not a cheap task-set revision. It includes
unrelated editable graph facts, can be large, and cannot prove a task query's complete
material membership merely by listing the IDs returned by a page. Prefer a Workbench
task-planning collection revision advanced atomically by every material writer, plus
owner-defined row/lifetime witnesses where needed. Persist it per project/lifetime and
named planning scope; never one global workspace revision. A bounded exact-set witness
is acceptable only if its full membership and all participating writer exclusion are
proved and measured. Decide between these representations in card B, not incidentally
inside the tool.

Before freezing the witness, enumerate native/API/tool/background/import/recovery
writers of `ProjectObjectRecord`, task metadata, parent/link relationships and any
other selected material columns. Include typed task create/update, lifecycle changes,
estimates/pricing, reparent/delete/copy/move, cross-project transfer, workflow output,
assignment changes, compensation and import/package/profile-transfer paths. Starting
anchors: `ProjectStructureCanonicalTaskMutationPolicy.cs`,
`ProjectStructureTaskCreationService.cs`, `ProjectStructureTaskEditCompensationService.cs`,
`ProjectStructureTaskPricingCommitService.cs`, `ProjectStructureWorkItemAssignmentRevisionService.cs`,
`Workbench/ProjectWorkbenchLifecycleService.cs`, `ProjectWorkbenchCommandService.cs` and
`ProjectWorkbenchCrossModuleMutations.cs`. The generic-task guard correctly blocks
several broad APIs; it does not prove that all non-generic writers advance a new witness.
Personal Gantt ordering need not invalidate a task plan unless ordering is declared a
material input. Material projection and writer inventory must agree exactly.

Projects P02a exposes a coherent original definition snapshot; its lifetime admission
alone cannot fence the objective at publication. Add a Projects-owned typed enlisted
definition guard, validating original lifetime and revision using the same physical
database transaction/ordered owner lock as Workbench's final publication. Workbench
must not query `ProjectsDbContext` or copied Projects entities. Existing
`ProjectMutationAuthority`, `ProjectProcessExecutionMutationService` and
`ProjectWorkflowMutationService` establish source-before-owner acquisition/revalidation.
The new guard must fit their canonical ordering; no nested acquisition of the same
non-reentrant project lock. For capture, obtain a coherent bounded owner read through
an explicit read participant/connection boundary, or a proved stabilized read protocol;
two unrelated reads are not automatically one snapshot. Never hold this read snapshot
over provider/model work.

`ProjectStructureTaskCreationService.cs:43` prices, ensures a backlog, creates a task,
attaches a resource and inserts row ordering in separate calls, with compensation.
`ProjectStructureGanttRowOrderService.cs:51` adds an in-process stripe/advisory lease,
not a shared database transaction across those calls. Therefore a loop over
`project_task_create` is not atomic plan publication. Implement a small Workbench-owned
batch publisher using context-level canonical task validation/mutation primitives;
reuse/extract those primitives without copying canonical task rules or adding methods
to a growing execution partial. Commit all bounded task rows, required relationships,
material revision advancement and exact result receipt together. If existing task
semantics require a cross-module/external operation, the initial batch must reject that
proposal shape until its owner transaction contract exists. Manual unassigned tasks
may be the first supported shape; this restriction must be explicit in tool schema/UI,
not silently discard requested assignments or authoritative pricing.

The transaction sequence is current source/claim admission, ordered owner locks,
exact operation replay check, material and target validation, all mutations, final
authority fence, exact bounded receipt, commit. Final order must be checked against
deletion/hierarchy/transfer before implementation. No provider/model, file, human,
network external-service wait or speculative pricing lookup belongs in it. Database
I/O is necessary and bounded. A known committed result survives projection/advisory
cleanup failure; an unknown commit goes to exact reconciliation, never semantic replan.

P02b/lifecycle06 exact receipt identity, expiry/epoch and disclosure rules are a
prerequisite to promising durable publication replay. The Agent journal alone is not
an atomic Workbench receipt. Lookup must recheck current permissions for the historical
accepted payload. A changed-basis result contains sanitized reason/identities and
available revisions, not unauthorized current facts or secrets.

| Source | Can strict publication be validated atomically now? | Required closure |
| --- | --- | --- |
| Projects definition plus Workbench task plan in one canonical PostgreSQL dataset | Not yet: definition read exists, but enlisted content guard, full task writer perimeter and plan publisher are still required. | Cards B/C, owner lock order, dataset/profile fence and receipts. |
| Current Process dispatch authority | Existing enlisted authority uses exact transaction, root lock/row locks and claim/attempt checks (`EfProcessWorkflowDispatchAuthority.cs:13`). | Preserve it; add no product reference to Process runtime. It proves authority, not mutable business facts. |
| CRM workforce capacity, assignment/rate/skills facts | Not proved: current mutable writers in05 do not all implement a source-content/set witness or enlistment guard. | CRM-owned revision/membership/overlap predicate and all writer participation, with a typed enlisted port. A capacity search result is not a reservation or an availability guarantee. |
| Resource metadata, pricing/provider/workspace configuration | Owner coverage is partial; a cross-owner lookup followed by publication leaves a race. | Close relevant05 owners first or exclude the source from that operation and reject proposals that require it. |
| Files, external MCP/provider data, another database/profile | Cannot atomically validate with this SQL commit by merely re-reading before it. | Provider conditional/idempotent operation/reservation, an explicit staged saga with different guarantees, or a supported historical observation operation. Do not advertise strict atomic currentness. |
| Immutable published Process/Workflow/prompt version | Version can remain pinned while retained; authorization and retention still matter. | Validate exact immutable reference and current use/disclosure rights. Never refresh its version as a data policy side effect. |

## Durable execution and change response

Persist a versioned decision attempt before dependent reasoning starts: trusted source
execution/step/occurrence, lineage, attempt ordinal, effective policy, basis reference
and hash, prepared/expiry times, budget consumed and status. Allocate it with CAS and a
unique owner key. A racing worker receives the same attempt. A retry loads this row;
it does not reread mutable facts. New semantic attempts consume the lineage budget
atomically before generating new input. A random client attempt ID is not admission.

The basis prepare boundary must precede reasoning. A model invoking a read tool after
already forming its plan is not proof that it reasoned from that input. The governed
planner loads the durable captured facts into the actual proposing call, or uses an
explicit prepare-then-propose step graph. The publisher accepts only a proposal bound
to that captured input and attempt. Additional data-dependent tools must either be
declared/enrolled before proposal or cause a new admitted basis; unsupported reads
cannot silently become untracked material input.

| Runtime | Durable seam and required work | Resume rules |
| --- | --- | --- |
| Agent | Existing tool journal retains proposal/digest/approval/result. `WorkflowBackedAgentExecutionCheckpointBridge.cs:34` captures only when PendingApprovals exist; `:194` writes filesystem checkpoint/index and shadow payload, then separate run metadata. `NullAgentExecutionCheckpointBridge` returns null. Add typed decision-change state to existing durable run/journal ownership; extend checkpoint binding only when session continuation actually needs it. | Pause may preserve the rejected proposal for review without promising an arbitrary provider-session resume. New replan can start a new admitted planning call from retained basis/proposal. If continuing a provider session is required, missing/corrupt/unavailable actual checkpoint is a typed block; null or display metadata is never success. Retain existing exact approval validation and current source disclosure checks. |
| Process | Runtime state has WaitingForUser/Blocked/WaitingApproval and recovery receipts; `ProcessRecoveryClassifier.cs:21` restricts safe/idempotent replay and counts diagnostics. Prepared launch and assignment already retain immutable evidence. | Persist a distinct material-basis change reason and reference through the runtime commit/recovery path. Release dispatch lease; acquire a fresh valid claim before any effect. Do not route changed basis through generic transient/executor retry. Explicit replan requires a new admitted decision attempt and compatible background same-source identity. Prior completed step effects remain history. |
| Workflow | `WorkflowCheckpointFactory.cs:13` emits MetadataOnly/NotSupported. Real backend state is stored via `MafJsonCheckpointStoreAdapter.cs:35`, linked by `MafHumanInputCheckpointCorrelator` and external-request continuation/resume-boundary stores. | Only supported durable backends may offer resumable pause. Persist effective policy/attempt/basis reference with the trusted node occurrence/checkpoint link and content hash. Continue through current external-response authorization, lease and checkpoint validation. A stale/changed payload requires a new proposal/request approval; never mutate the accepted external-response operation. |

Do not reuse authority denials as freshness events. Revoked source, retired lifetime,
invalid Process claim, unsupported operation, malformed basis or inaccessible owner is
a denial/block, not a replan permission. `MaterialBasisChanged` and `BasisExpired` can
use the configured response. A stale target keeps its original write expectation and
needs explicit reconciliation. `OutcomeUnknown` enters receipt reconciliation and
cannot spend a replan budget while an effect may already exist.

Bound budgets durably by lineage across restarts, nested calls and engine adapters.
Executor retry counters are separate. Work duration excludes deliberate human wait;
absolute basis/operation expiry does not stop while waiting. Automatic replan is zero
by default. Architecture02's 100 intents, 64 fact dependencies plus one task set,
32-KiB inline basis metadata, at most two explicitly permitted replans and proposed
age/work bounds are sizing candidates, not measured production limits. Card C must
measure and freeze caps; reject oversize before provider spend/transaction allocation.
Owner basis retention must cover allowed review/reconciliation windows with finite
per-dataset/run quotas. Expired basis cannot be reconstructed from current data under
the old ID. Existing legacy runs remain readable; missing original evidence is not
backfilled with today's policy or data. They require explicit newly admitted work for
this new governed publication contract.

## Dependency-ready work cards

Each card is a separate review/commit unit. Design-ready means its proposed boundary is
specified here, not that its prerequisite evidence is green. No card may advertise a
mode before its actual execution/backend path passes. A reopened source schema, policy
resolver, digest, claim or membership perimeter invalidates all dependent proofs.

| Card | Outcome and exact boundary | Prerequisites / progression gate |
| --- | --- | --- |
| A — neutral policy | Add minimal validated vocabulary/resolver in existing SharedKernel, versioned canonical policy serialization, typed capability rejection. Pure unit tests; no services/EF/runtime dependencies. | Confirm actual project graph and two-mode decision. Review no new universal coordinator/registry. Independent of P02b; production default not enabled yet. |
| B — material witness | Workbench task-set descriptor/revision and writer inventory; Projects enlisted definition guard; bounded coherent basis capture in Workbench. Close same-project lock order, deletion/transfer/ABA, scope completeness and all material writers. | Projects snapshot/CAS P02a plus current05 Workbench/Projects ownership evidence. Source inventory and deterministic interleaving tests must pass before claiming RequireCurrent. Wider CRM/resource sources remain explicit P05 prerequisites. |
| C — atomic publisher | Context-level canonical task primitives and bounded all-or-none plan publisher, typed result/error, original target conditions, exact owner receipt and lookup, measured limits/indexes. | B and P02b/lifecycle06 receipts. Prove no external calls in transaction, lost reply exact recovery, no partial task/attachment/order effects, disjoint-project concurrency and known-commit result truth. No automatic SQL retry of a semantic proposal. |
| D — owner settings | Workspace default, Agent definition override, Process definition/step override, Workflow definition/node override; typed transport/editor/copy/hash paths. Use existing owner concurrency and close its missing-expectation holes where present. | A; each owner save perimeter closure from05. These subcards can proceed independently with non-overlapping owners. Persisted settings can be present but unsupported launch combinations must reject until E/F/G. No editable raw JSON as the only product surface. |
| E — durable decision admission | Independent small admission/change-response services at runtime boundaries; typed attempt/ref carried by existing execution records, immutable policy/basis, CAS budget, replay/replan distinction, versioned serializers. Workbench prepare/propose tool registration is a dedicated provider. | A/B; C before publication enabled. Binding/digest review against actual journal and original-source authority. Prove provider inputs come from the admitted basis. No new partial file on execution service. |
| F — Process integration | Carry policy from published definition through prepared launch/assignment and actual Agent/Workflow dispatch. Persist changed-basis reason/review state; distinguish transport recovery from a newly authorized decision attempt without changing original Process lineage. | D-Process/E, current claim fencing. Agent and Workflow adapters compose outward in Modules.Processes. Test launch/restart/claim rotation, immutable assignment repair restrictions and background same-source uniqueness. |
| G — Workflow integration | Definition/node policy round-trip and hash; launch intent/run binding; occurrence-based basis; real backend checkpoint and resume binding; external-response approval renewal on changed proposal. | D-Workflow/E and backend capability proof. Unsupported backend rejects pause before launch. Test restart, nested Process/Agent policy ceiling, malformed/checkpoint mismatch and no duplicate effect. |
| H — shipped controls and runtime journeys | Agent, Process step/launch, Workflow node/launch and workspace settings show supported modes, effective value/source, change response and budget; review shows original basis/proposal plus sanitized change reason and explicit fail/new-plan actions. Actual API/tool readback matches UI. | D/E/F/G and C for task-plan publication. Components MCP before UI composition, existing host/draft seams, no fresh-policy or fresh-target substitution while saving. Native and actual runtime journeys in all three engines required; Projects-only proof cannot close P07. |

A may be implemented once its architecture checkpoint is accepted. B and D need their
listed owner work/inventories before source semantics are frozen. E is not a reason to
grow AgentFrameworkWorkspaceExecutionService: orchestration forwards to a separately
testable service with explicit inputs and existing journal ports. F/G must independently
prove adapter wiring; passing the pure policy resolver is insufficient. H is not an
optional demonstration: it is the user-visible completion of configurable behavior.

## Deterministic proof plan

Use existing standard Release outputs and the explicitly isolated PostgreSQL18 fixture;
parent serializes builds and runs. No new stack per card. The following are proposed
test method names and minimum source-case counts, not discovered or executed tests.
Keep barrier-based race tests free of timing sleeps. Inject clock/provider completion
barriers and owner failure points; no live paid provider is needed for deterministic
runtime proof. A separate actual host smoke validates real registration/adapters.

| New owning test topic / minimum cases | Deterministic cases |
| --- | --- |
| `DecisionDataPolicyTests` — 8 unit Facts | `OwnerMandateCannotBeWeakenedByInvocation`; `UnsupportedPauseIsRejected`; `UnknownModeIsRejected`; `NestedBudgetUsesTightestAllowance`; `RunSnapshotUsesOneAdmittedBasis`; `StepSnapshotRefreshesOnlyForNextLogicalStep`; `RetryRetainsOriginalPolicyAndBasis`; `SemanticReplanRequiresNewAttemptAndApproval`. |
| `ProjectTaskPlanBasisTests` — 8 database Facts | `ObjectiveChangeRejectsPublication`; `InsertedTaskInvalidatesCompleteSet`; `DeletedTaskInvalidatesCompleteSet`; `MaterialTaskEditInvalidatesBasis`; `UnrelatedPresentationChangeDoesNotInvalidate`; `SameIdRecreationRejectsOldBasis`; `TruncatedMembershipCannotBecomeBasis`; `RevokedSourceIsDeniedBeforePublication`. Extend with one case for each distinct writer family identified in B; eight is a floor, not perimeter closure. |
| `ProjectTaskPlanPublicationTests` — 6 database Facts | `BatchCommitsAllTasksAndExactReceipt`; `MidBatchFailureLeavesNoEffects`; `LostReplyReplaysExactAcceptedResult`; `SameIntentDifferentBasisConflicts`; `DifferentProjectsDoNotSharePublicationLock`; `UnknownCommitCannotTriggerReplan`. |
| `AgentDecisionPolicyIntegrationTests` — 6 Facts | `ProviderReceivesPersistedBasis`; `RetryAfterRestartDoesNotRefresh`; `ChangedBasisPersistsReviewWithoutLease`; `MissingCheckpointCannotClaimSessionResume`; `ReplanCreatesNewDigestAndApproval`; `BudgetCannotResetThroughNewRunId`. |
| `ProcessDecisionPolicyIntegrationTests` — 6 Facts | `PreparedLaunchSealsResolvedPolicy`; `AssignmentRepairCannotChangePolicy`; `NextStepCapturesCurrentBasis`; `RetryReusesOriginalDecisionAttempt`; `RotatedClaimRejectsPublication`; `ReviewResumeUsesFreshClaimAndNewAuthorizedAttempt`. |
| `WorkflowDecisionPolicyIntegrationTests` — 6 Facts | `LaunchKeyRejectsChangedPolicy`; `NodeOccurrencePinsBasisAcrossExecutorRetry`; `MetadataBackendRejectsPause`; `RestartRestoresExactPolicyBasisAndBudget`; `ChangedProposalRequiresNewExternalRequestApproval`; `NestedExecutionCannotRelaxPolicy`. |
| `DecisionPolicyEditorTests` — 4 component Facts | `AgentPolicyRoundTripsWithoutLosingOriginalRevision`; `ProcessStepOverrideAppearsInCompiledProjection`; `WorkflowNodeOverrideSurvivesSaveAndReload`; `UnsupportedModeCannotBeSubmitted`. |
| `DecisionPolicyRuntimeJourneyTests` — 3 host/browser journeys | `AgentPauseThenExplicitReplan`; `ProcessStepSnapshotAndRestart`; `WorkflowPinnedRunAndStrictPublicationConflict`. Exercise actual registered tools/launch path, edit data from an independent session, inspect durable outcome, and prove target unchanged before review. |

These named topics total 47 proposed cases before writer-perimeter expansion. Place
unit/integration/component/browser topics in the existing corresponding test projects;
new production unit seams must be testable without constructing the entire workspace
runtime. Freeze each card's exact filter/count after implementation and build-backed
discovery; do not claim a planned count as discovered. Existing targeted regression
anchors already inspectable from source are:

* `ProcessLaunchAtomicCommitTests`: 10 cases (6 Facts plus two 2-row theories).
* `AgentApprovalDecisionValidationTests`: 8 Facts.
* `AgentApprovalCheckpointRecoveryTests`: 8 cases (6 damage rows plus 2 CAS-order rows).

Use `FullyQualifiedName~<topic>` per owning test project; confirm discovery before run.
Add only relevant existing journal/background reservation, prepared-launch persistence,
Workflow checkpoint/external-response/idempotency and canonical-task graph topics for
the changed card, with their own source/discovery counts recorded at that gate. Do not
launch all runtime tests merely because policy is cross-cutting. A dependency/claim/
storage-schema change triggers the affected broader topic; final release/merge remains
the bundle's broad stable gate. Every protected-source card runs portability-static,
reviews added/stale findings, refreshes only intentional baseline deltas, then passes
enforcement without write-baseline. UI proof uses the supported large desktop viewport,
normal state plus review/settings overlay, keyboard actions and visible error states.

## Bounded unresolved gates

1. B must choose/measure complete-set representation and prove every material writer;
   no current full-graph read or global lock is an automatic substitute.
2. C must settle the atomic canonical-task shape and exact receipt cap. Existing staged
   task creation cannot be reused as though it were one transaction. Resource-attached
   or capacity-dependent plans remain blocked until their relevant owner guards exist.
3. E/F must specify the versioned semantic-attempt extension to same-source admission
   while preserving existing run/step replay and claim identity. Current source-keyed
   admission cannot create a new attempt by an ordinary repeated dispatch call.
4. E/G must freeze durable pause storage, supported backend capability and orphan/corrupt
   checkpoint recovery. Agent filesystem and SQL journal updates are not one atomic
   database commit; persist a state that is recoverable and never claim continuation
   availability until every required artifact validates.
5. Freeze retention, quotas, exact normalized basis content and policy/budget accounting
   against06 before enabling automatic replan. No historical current-data reconstruction.
6. Wider owner policy controls only become supported when their material data source
   can enforce the promised semantics. Track CRM, workspace, Agent configuration,
   Workflow authoring, resources and external owners under05; do not close the user
   request by declaring the initial task-plan slice representative of completed work.
