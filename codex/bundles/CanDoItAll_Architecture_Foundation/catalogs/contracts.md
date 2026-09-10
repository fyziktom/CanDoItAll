# Semantic communication contracts

Target operations and required ports, not a quota of interfaces or an automatic runtime permission manifest. Reuse existing compatible APIs. The list is not an implementation plan.

## CON-001

**Name:** Agent catalog / batch lookup

**Mode:** query

**Input contract:** Scope, a filtered page cursor or explicit AgentIds, and requested public attributes.

**Result contract:** AgentSummary: identity, label, technical status, capability hints and source revision; per-item availability where applicable.

**Invariants and failure:** Bounded pagination/batches without N+1; no secrets, system prompts or EF AgentDefinition. A listing is not execution admission. A missing CRM binding does not create a technical agent.

**Source ids:** SRC-009; SRC-010; MAP-05

**Current anchor:** IAgentReferenceDataProvider; IAgentFrameworkWorkspaceService.ListAgentsAsync discovery anchors.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-AGENTS

**Caller modules:** MOD-CRM; MOD-STRUCTURE; MOD-PROCESSES; MOD-SCHEDULER; MOD-WORKSPACE

**Implementation modules:** MOD-AGENTS

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-AGENTS. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-002

**Name:** Agent definition commands

**Mode:** command

**Input contract:** Typed create/edit/archive intent; expected revision for changes and explicit OperationId for a newly retry-safe route.

**Result contract:** Committed with agent identity/revision; Conflict, Validation, Denied or NotFound; downstream synchronization separately.

**Invariants and failure:** CRM onboarding requests creation from Agents instead of storing a definition. A successful commit is not relabeled failed save when CRM projection synchronization fails.

**Source ids:** SRC-009; MAP-06

**Current anchor:** Agent editor commands and technical-agent bridge: rebaseline after Agents stabilization.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-AGENTS

**Caller modules:** MOD-CRM; BND-API; MOD-AGENTS

**Implementation modules:** MOD-AGENTS

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-AGENTS. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller -> owner contract; owner application implements it; composition wires adapters. Neutral conversation UI is not a direct caller; the product-specific adapter invokes the domain contract.

## CON-003

**Name:** Governed agent execution

**Mode:** long_running_command

**Input contract:** Agent identity and pinned configuration/selection policy; verified source authority; bounded input; OperationId, purpose and execution target.

**Result contract:** Accepted/Existing with RunId and operation receipt; stream/status handle; explicit pre-admission rejection.

**Invariants and failure:** Admission cannot depend on a live component; callers cannot forge a more privileged purpose. External inference occurs outside database transactions. Reconcile an unknown launch rather than blindly replaying it.

**Source ids:** SRC-009; SRC-003; MAP-09

**Current anchor:** AgentChatExecutionOrchestrator; AgentFrameworkProcessExecutionAdapter; executor resolver.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-AGENTS

**Caller modules:** MOD-PROCESSES; BND-WORKFLOWS; MOD-TESTLAB; MOD-AGENTS

**Implementation modules:** MOD-AGENTS

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-AGENTS. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller -> owner contract; owner application implements it; composition wires adapters. Neutral conversation UI is not a direct caller; the product-specific adapter invokes the domain contract.

## CON-004

**Name:** Agent approval / cancellation / status

**Mode:** command_and_query

**Input contract:** RunId/OperationId; exact approval identity, payload fingerprint, expected revision and decision; cancellation reason.

**Result contract:** Current durable state, decision receipt, conflict/expired/denied; cancellation-requested distinct from terminal cancelled.

**Invariants and failure:** Recheck authority, scope, state and revision. A changed request needs new approval. Closing UI is not cancellation.

**Source ids:** SRC-003; SRC-009

**Current anchor:** Existing source authority continuation, tool receipts and runtime approval boundaries.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-AGENTS

**Caller modules:** MOD-PROCESSES; BND-API; MOD-AGENTS

**Implementation modules:** MOD-AGENTS

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-AGENTS. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller -> owner contract; owner application implements it; composition wires adapters. Neutral conversation UI is not a direct caller; the product-specific adapter invokes the domain contract.

## CON-005

**Name:** AI workload cost quote

**Mode:** query

**Input contract:** Agent/definition reference, workload/expected usage with units, scope, optional pinned provider choice and requested currency without implicit FX.

**Result contract:** QuoteId, source revisions, tariff components/currencies/units, assumptions, confidence/range/unpriced state, validity and chosen/fallback target.

**Invariants and failure:** Do not store a master AI price in CRM. Distinguish missing usage/prices from explicit zero. Currency conversion requires an FX rate. A quote is not budget reservation.

**Source ids:** SRC-007; USER-001

**Current anchor:** ProviderPricingModels and task cost-strategy discovery; a unified public quote contract is proposed.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-PROVIDERS

**Caller modules:** MOD-CRM; BND-WORK; MOD-PROCESSES; BND-WORKFLOWS

**Implementation modules:** MOD-PROVIDERS

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-PROVIDERS. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-006

**Name:** Provider administration

**Mode:** command_and_query

**Input contract:** Profile identity, typed configuration without resolved secrets, SecretReference, expected revision and supported-model publication policy.

**Result contract:** Sanitized profile summary, validation/probe result or committed revision.

**Invariants and failure:** The owner validates transport/model/purpose compatibility. A probe is not an unconditional save; snapshot-refresh failure after commit is a separate outcome.

**Source ids:** SRC-022; SRC-007

**Current anchor:** IProviderAdministrationService a IProviderRuntimeAdministrationService discovery anchors.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-PROVIDERS

**Caller modules:** MOD-AGENTS; MOD-WORKSPACE; BND-API

**Implementation modules:** MOD-PROVIDERS

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-PROVIDERS. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-007

**Name:** Provider execution profile lease

**Mode:** query_admission

**Input contract:** Trusted operation scope, profile identity, required capability and pinned fingerprint.

**Result contract:** Immutable runtime profile/configuration revision lease; unavailable/changed/denied; short-lived purpose-specific secret resolution is separate.

**Invariants and failure:** Verify revision and profile generation at use time; no secrets in singleton caches. Scope changes must not retarget prepared dispatch.

**Source ids:** SRC-009; SRC-007

**Current anchor:** CanonicalProviderRuntimeProfileSnapshotService; IProviderRuntimeProfileSource.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-PROVIDERS

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; BND-SIMPLECHATS

**Implementation modules:** MOD-PROVIDERS

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-PROVIDERS. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-008

**Name:** Shared-provider publish/import/sync

**Mode:** command_and_query

**Input contract:** Publication/source/import identities, scope, allowed models/capabilities, expected revision, remote identity and network policy.

**Result contract:** Versioned advertised catalog, import state, routing identity, explicit remote-unavailable/revoked states and operation receipt.

**Invariants and failure:** Do not copy upstream secrets or enlarge the published set. Adapters verify remote claims. Do not charge for one relay attempt twice.

**Source ids:** SRC-022; SRC-007

**Current anchor:** SharedProviders.Http a ProviderManagement publication/source/import lifecycle.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-PROVIDERS

**Caller modules:** BND-API; MOD-AGENTS

**Implementation modules:** MOD-PROVIDERS

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-PROVIDERS. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-009

**Name:** Usage evidence and allocation query

**Mode:** query_and_owned_append

**Input contract:** Invocation identities/run references, currency/time filters and explicit allocation basis; only trusted execution adapters append evidence.

**Result contract:** Observed usage, frozen tariff, cost completeness/provenance and aggregation coverage; source identities for deduplication.

**Invariants and failure:** Appending evidence does not reprice history using today's tariff. Parent-run and child-invocation totals must not double-count. Resolved secrets or bodies are not standard query results.

**Source ids:** SRC-007; SRC-009

**Current anchor:** Provider request history, audit/usage projections.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-PROVIDERS

**Caller modules:** MOD-AGENTS; MOD-CRM; BND-WORK; MOD-PROCESSES; BND-SIMPLECHATS

**Implementation modules:** MOD-PROVIDERS

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-PROVIDERS. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-010

**Name:** CRM party/resource catalog

**Mode:** query

**Input contract:** Scoped identities, bounded search/page, role/availability filter and query purpose.

**Result contract:** Sanitized Party/ResourceSummary, owned revision and separate technical AgentReference.

**Invariants and failure:** Confidential fields require special-purpose detail access. Technical AI data comes from Agents, not a stale copy treated as master. Availability is a hint, not a reservation.

**Source ids:** SRC-010; MAP-08

**Current anchor:** Directory/Workforce query services a privacy-safe runtime query.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-CRM

**Caller modules:** MOD-PROJECTS; BND-WORK; MOD-PROCESSES; MOD-SCHEDULER; MOD-STRUCTURE

**Implementation modules:** MOD-CRM

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-CRM. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-011

**Name:** CRM domain commands

**Mode:** command

**Input contract:** Typed party/contact/recruitment/governance intent, verified actor and expected revision.

**Result contract:** Owner result with identity/revision/audit and validation/conflict outcomes.

**Invariants and failure:** Route technical agent changes to CON-002. CRM executes its own changes with required indexing, activity and lifecycle effects. Individual AI-facing operations require the narrower contracts and policies in the revision extension.

**Source ids:** SRC-010; MAP-08

**Current anchor:** CRM application services; HR tool adapter.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-CRM

**Caller modules:** MOD-PROJECTS; BND-API; MOD-AGENTS

**Implementation modules:** MOD-CRM

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-CRM. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-012

**Name:** AI resource projection/binding reconcile

**Mode:** owned_projection_command

**Input contract:** Trusted AgentReference snapshot/tombstone and source revision, plus explicit CRM resource-enrolment/binding policy.

**Result contract:** Applied/AlreadyApplied/StaleIgnored/Conflict/ScopeMismatch, CRM binding identity and synchronization state.

**Invariants and failure:** Only CRM writes bindings. Separate technical and CRM-owned fields. Never clone a missing agent or revive an archived binding without explicit intent.

**Source ids:** SRC-010; MAP-08; MAP-06

**Current anchor:** AiResourceBinding and technical bridge; replace the current cross-writer with an adapter.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-CRM

**Caller modules:** MOD-AGENTS; BND-COMPOSITION

**Implementation modules:** MOD-CRM

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-CRM. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-013

**Name:** Capacity reservation

**Mode:** command_and_query

**Input contract:** Resource reference, interval/time zone, capacity units, assignment/correlation identity, expected revision and reserve/change/release intent.

**Result contract:** Reserved/Conflict/Unavailable/Released or explicitly unsupported; stable ReservationId.

**Invariants and failure:** Staffing capacity is not a provider technical limit. Coordinate mandatory reservation plus assignment atomically or report pending. Do not claim a complete reservation engine already exists.

**Source ids:** MAP-08

**Current anchor:** CapacityBlock/StaffingRequest are discovery anchors; a complete reservation API is a target design.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-CRM

**Caller modules:** BND-WORK; MOD-PROCESSES

**Implementation modules:** MOD-CRM

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-CRM. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-014

**Name:** Project participation

**Mode:** command_and_query

**Input contract:** ProjectReference, PartyReference, commercial/staffing role, interval/rate metadata and expected revision.

**Result contract:** Role assignments, committed revision, conflict/denied and project-lifecycle validation.

**Invariants and failure:** Do not hide work-item performers inside an ambiguous generic role. CRM owns the relationship; Projects owns the project. Deletion/transfer uses owner participants.

**Source ids:** MAP-01; MAP-08

**Current anchor:** ProjectPartyIntegrationContracts/ProjectPartyAssignment.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-CRM

**Caller modules:** MOD-PROJECTS; MOD-STRUCTURE

**Implementation modules:** MOD-CRM

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-CRM. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-015

**Name:** Project lookup

**Mode:** query

**Input contract:** Scope and project identities, bounded page/phase/portfolio filters, and required public attributes.

**Result contract:** ProjectSummary, revision and access-safe existence/lifecycle result.

**Invariants and failure:** Do not use ListAll plus FirstOrDefault in every consumer. Map NotFound versus Denied without existence leakage according to API policy.

**Source ids:** SRC-011; MAP-01

**Current anchor:** ProjectsService; proposed narrow catalog.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-PROJECTS

**Caller modules:** MOD-STRUCTURE; MOD-RESOURCES; MOD-TESTLAB; MOD-CRM; MOD-SCHEDULER; MOD-PROCESSES

**Implementation modules:** MOD-PROJECTS

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-PROJECTS. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-016

**Name:** Project lifecycle orchestration

**Mode:** long_running_command

**Input contract:** Project identity, expected lifecycle revision, typed delete/archive/export/transfer intent and explicit dispositions.

**Result contract:** Durable operation identity, effect plan, participant receipts and committed/pending/partial/blocked states; recoverable status query.

**Invariants and failure:** Participants change only their own data. Lifecycle blocks new target mutations as appropriate. No global cascade beyond the explicit plan.

**Source ids:** MAP-01; MAP-10

**Current anchor:** Project deletion/transfer participant mechanism.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-PROJECTS

**Caller modules:** BND-API; MOD-STRUCTURE; MOD-WORKSPACE

**Implementation modules:** MOD-PROJECTS

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-PROJECTS. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-017

**Name:** Project hierarchy

**Mode:** command_and_query

**Input contract:** Parent/child project references, typed hierarchy mutation and expected revisions.

**Result contract:** Validated hierarchy/reference edges; cycle/conflict/denied.

**Invariants and failure:** Structure displays the edges but is not another master. Preserve current DAG/cardinality policy and related-project navigation.

**Source ids:** SRC-011; MAP-01

**Current anchor:** ProjectHierarchyLink; hierarchy bridge/contributor.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-PROJECTS

**Caller modules:** MOD-STRUCTURE; BND-API

**Implementation modules:** MOD-PROJECTS

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-PROJECTS. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-018

**Name:** Structure read / invocation read

**Mode:** query

**Input contract:** Project identity, explicit read source, node filters/pages/bounds, requested coverage, and an internally held invocation-snapshot handle only.

**Result contract:** Immutable nodes/links, per-source revisions, coverage/omissions, stale/unavailable states and allowed-action hints.

**Invariants and failure:** InvocationSnapshot fails closed without fallback; HTTP has no in-process snapshot handle. Mutations use current canonical state. Read-side projection rebuild performs no owner writes.

**Source ids:** SRC-008; SRC-003; SRC-025

**Current anchor:** ProjectStructureInvocationSnapshotReadDispatcher; IProjectStructureRuntimeGateway.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-STRUCTURE

**Caller modules:** MOD-AGENTS; MOD-PROCESSES; BND-WORKFLOWS; BND-API

**Implementation modules:** MOD-STRUCTURE

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-STRUCTURE. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller -> owner contract; owner application implements it; composition wires adapters. Neutral conversation UI is not a direct caller; the product-specific adapter invokes the domain contract.

## CON-019

**Name:** Native structure authoring

**Mode:** command

**Input contract:** Project/parent NodeKey, typed kind-specific payload, expected revisions/lease where policy requires, and explicit create/update/reclassify/remove intent.

**Result contract:** Node identity/revision, typed validation/conflict/scope failures; separate metadata and content outcomes.

**Invariants and failure:** Canonical task kinds cannot bypass Work Management. Generic notes payloads cannot create file assets. Callers cannot set system-ownership flags.

**Source ids:** SRC-025; SRC-004; USER-001

**Current anchor:** ProjectWorkbenchService/ProjectStructureAgentService; adapt rather than discard.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-STRUCTURE

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES; BND-API

**Implementation modules:** MOD-STRUCTURE

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-STRUCTURE. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-020

**Name:** Structural links

**Mode:** command_and_query

**Input contract:** Endpoint NodeKeys/EntityReferences appropriate to relation kind, expected endpoint/graph revisions and idempotent operation identity.

**Result contract:** Created/Existing/Removed link identity and revised graph; unsupported-owner or endpoint failure.

**Invariants and failure:** Endpoint authority is not transferred. Route source-owned project/process/dependency relationships to their owner. Validate scope, lifecycle, cardinality and cycles according to kind.

**Source ids:** USER-001; MAP-02; MAP-04

**Current anchor:** ProjectWorkbenchRelationService; existing link commands.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-STRUCTURE

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES; BND-API

**Implementation modules:** MOD-STRUCTURE

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-STRUCTURE. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-021

**Name:** Ensure structure contribution

**Mode:** command

**Input contract:** Producer namespace, trusted run/step origin, stable OutputKey, target binding, typed CreateNative/AttachReference/CreateAsset/CreateTask/Link intent, normalized payload hash and expected target revision.

**Result contract:** Durable ContributionReceipt: accepted/applied/existing/pending/blocked/suppressed/conflict; created references, revisions, effect state and source origin.

**Invariants and failure:** Follow architecture/05. Required is not an authority bypass. Same key with different payload conflicts; do not revive tombstones; recheck current scope. Native writes and receipts commit atomically.

**Source ids:** SRC-025; SRC-027; USER-001

**Current anchor:** Existing gateways and workflow executors are entry points; a uniform protocol must not replace typed commands.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-STRUCTURE

**Caller modules:** MOD-AGENTS; MOD-PROCESSES; BND-WORKFLOWS; MOD-RESOURCES; MOD-PROMPTS; MOD-TESTLAB

**Implementation modules:** MOD-STRUCTURE

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-STRUCTURE. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-022

**Name:** Asset binding and content lifecycle

**Mode:** command

**Input contract:** Target node/parent, trusted staged-content handle or authorized StorageObjectReference, MIME/name/revision, replace/attach intent and operation identity.

**Result contract:** AssetBinding/node identity, content revision and finalized/pending/failed-cleanup state.

**Invariants and failure:** Bytes are outside the database transaction: use staging, finalization and compensation. Do not write file contents into Notes. Native SimpleNote is a different kind. Revalidate source paths.

**Source ids:** SRC-004; SRC-005; SRC-025

**Current anchor:** ProjectAssetCreationService/StorageService/ReplaceObjectMedia a FileTools storage pipeline.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-STRUCTURE

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES; BND-API; MOD-RESOURCES

**Implementation modules:** MOD-STRUCTURE

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-STRUCTURE. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-023

**Name:** Canonical work-item commands

**Mode:** command_and_query

**Input contract:** Typed task goal/description/schedule/acceptance/resource intent, project parent, expected revisions and operation identity.

**Result contract:** WorkItem detail/revision, validation/partial-compensation status and invariant-protected creation.

**Invariants and failure:** One owner serves canvas, Gantt and tools. Generic CreateNode must not bypass task-resource and assignment invariants.

**Source ids:** MAP-02; SRC-027

**Current anchor:** ProjectStructureTaskApplicationService, task creation/edit/resource cluster.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** BND-WORK

**Caller modules:** MOD-STRUCTURE; MOD-CRM; MOD-AGENTS; BND-WORKFLOWS; BND-API

**Implementation modules:** BND-WORK

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to BND-WORK. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-024

**Name:** Work assignment

**Mode:** command_and_query

**Input contract:** WorkItemRef; supported Human/Agent/Workflow/Process assignee reference; role/interval; expected assignment revision and optional reservation requirement.

**Result contract:** Assignment identity/revision plus reservation/quote references; committed/pending/conflict.

**Invariants and failure:** Not CRM ProjectParticipation. Preserve multiplicity and primary-role rules after reconciliation. Runtime launch is a subsequent operation, not an undocumented assignment-save side effect.

**Source ids:** MAP-02; MAP-08

**Current anchor:** WorkItemAssigneeService a ProjectWorkItemAssignmentMutationBridge.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** BND-WORK

**Caller modules:** MOD-CRM; MOD-STRUCTURE; MOD-AGENTS; BND-API

**Implementation modules:** BND-WORK

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to BND-WORK. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-025

**Name:** Gantt and dependency mutations

**Mode:** command

**Input contract:** WorkItem identities, typed scheduling/dependency/reorder intent, UTC instants plus time-zone/calendar meaning, expected revisions and explicit propagation scope.

**Result contract:** Updated plan slice/revisions, affected items, constraints/conflicts and invalidation hints.

**Invariants and failure:** Business scheduling is neither canvas coordinates nor cron. Do not silently shift unrelated tasks. Partial changes must be explicit; row ordering does not alter dependencies.

**Source ids:** MAP-02; MAP-03; USER-001

**Current anchor:** ProjectStructureGanttMutationService; GanttRowOrderService; dependency analysis.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** BND-WORK

**Caller modules:** MOD-STRUCTURE; MOD-AGENTS; BND-API

**Implementation modules:** BND-WORK

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to BND-WORK. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-026

**Name:** Plan estimate/baseline commit

**Mode:** command_and_query

**Input contract:** WorkItem/plan reference, versioned AI quotes and human-rate sources, assumptions/coverage and expected plan/resource revisions.

**Result contract:** An estimate with components/uncertainty or a committed immutable baseline; stale/conflict/unpriced outcomes.

**Invariants and failure:** A new tariff does not change an approved past baseline. Check quote-input revisions at commit. Do not add current actuals and baselines without an explicit aggregation basis.

**Source ids:** SRC-007; MAP-02

**Current anchor:** TaskEstimateRefresh, TaskPricingCommit/Persistence, CostStrategies.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** BND-WORK

**Caller modules:** MOD-STRUCTURE; MOD-CRM; BND-API

**Implementation modules:** BND-WORK

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to BND-WORK. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-027

**Name:** Process launch

**Mode:** long_running_command

**Input contract:** Definition reference/revision, normalized variables, verified actor/source authority, parent linkage, output-target binding and stable OperationId.

**Result contract:** Accepted/Existing RunId with frozen launch context, or explicit validation/readiness rejection.

**Invariants and failure:** Owner-controlled durable admission and start deduplication precede external effects. Bind the original project, not current UI selection. Subprocesses preserve parent authority and cancellation policy.

**Source ids:** MAP-09; USER-001

**Current anchor:** ProcessNodeService, subprocess launch coordinator, ProcessExecutionAdapter.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-PROCESSES

**Caller modules:** MOD-STRUCTURE; MOD-SCHEDULER; MOD-AGENTS; BND-API

**Implementation modules:** MOD-PROCESSES

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-PROCESSES. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-028

**Name:** Process run query/control

**Mode:** command_and_query

**Input contract:** Run identity/revision, bounded status/history/manifest filters, or explicit cancel/continue/recover intent.

**Result contract:** Durable runtime status, lineage, history and artifacts; accepted control or state conflict.

**Invariants and failure:** Not automatically a general direct runtime tool. UI labels do not change source state. Recovery cannot inherit scope from a newly opened project.

**Source ids:** SRC-003; MAP-09

**Current anchor:** Process runtime/contracts, run-record API and Blazor process workspace.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-PROCESSES

**Caller modules:** MOD-STRUCTURE; MOD-SCHEDULER; BND-API; MOD-TESTLAB

**Implementation modules:** MOD-PROCESSES

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-PROCESSES. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-029

**Name:** Workflow catalog/version lookup

**Mode:** query

**Input contract:** Definition/version references, lifecycle filters and scoped input schemas/capabilities.

**Result contract:** Typed summaries/details with actual versions, supported backend options and input schema.

**Invariants and failure:** Workbench must not read workflow EF entities. Active/default selection is owner policy; accepted runs remain pinned.

**Source ids:** SRC-026; MAP-09

**Current anchor:** IWorkflowCatalogService; backend selection support.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** BND-WORKFLOWS

**Caller modules:** MOD-STRUCTURE; MOD-PROCESSES; MOD-SCHEDULER; MOD-TESTLAB

**Implementation modules:** BND-WORKFLOWS

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to BND-WORKFLOWS. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-030

**Name:** Workflow launch/control

**Mode:** long_running_command

**Input contract:** Versioned workflow reference, validated input, requested backend/simulation policy, source authority/origin binding and stable operation identity; RunId for control.

**Result contract:** Launch receipt/RunId, execution state, approval/cancel results and output manifest.

**Invariants and failure:** Structure status-projection failure is not launch failure. Target GetStatus semantics are pure query; preserve current ApplyStatus behavior via an explicit refresh or projector.

**Source ids:** SRC-026; SRC-027; USER-001

**Current anchor:** IWorkflowLaunchService/IWorkflowRuntimeManager/IWorkflowRunStore; PS workflow node adapter.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** BND-WORKFLOWS

**Caller modules:** MOD-STRUCTURE; MOD-PROCESSES; MOD-SCHEDULER; BND-API

**Implementation modules:** BND-WORKFLOWS

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to BND-WORKFLOWS. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-031

**Name:** Process run result publication

**Mode:** durable_event_and_query

**Input contract:** Owner-specific RunId and source revision/sequence, ArtifactManifest, validated output roles and original destination binding.

**Result contract:** Identified runtime outputs and destination-delivery evidence; acknowledgment through a destination receipt rather than a change to the source result.

**Invariants and failure:** Processes owns only its own run result/manifest; CON-054 and CON-055 belong to Agents and Workflows. Late output must not overwrite a newer run or human edits.

**Source ids:** SRC-026; SRC-008; MAP-09

**Current anchor:** Process reports and result projection; target explicit lineage.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-PROCESSES

**Caller modules:** MOD-STRUCTURE; BND-WORK; MOD-TESTLAB; MOD-RESOURCES

**Implementation modules:** MOD-PROCESSES

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-PROCESSES. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-032

**Name:** Read-only structure contribution adapter

**Mode:** extension_query

**Input contract:** Scope, bounded requested coverage and stable source-cursor/revision requirements; no DbContext in the context.

**Result contract:** Read-only node/edge descriptors with source references, owner, schema, actions and provenance; missing/unavailable/partial coverage.

**Invariants and failure:** Producers provide owned query data; adapters translate it. Rebuilds do not change native nodes or start workflows. Action hints are not grants.

**Source ids:** MAP-02; MAP-04

**Current anchor:** IProjectStructureProjectionContributor; narrow the current ProjectionContext.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-STRUCTURE

**Caller modules:** MOD-STRUCTURE

**Implementation modules:** MOD-PROJECTS; MOD-CRM; MOD-PROCESSES; MOD-PROMPTS; MOD-RESOURCES; MOD-TESTLAB; MOD-AGENTS; BND-WORKFLOWS

**Extension implementers:** MOD-PROJECTS; MOD-CRM; MOD-PROCESSES; MOD-PROMPTS; MOD-RESOURCES; MOD-TESTLAB; MOD-AGENTS; BND-WORKFLOWS

**Data authority:** Every source module owns its entities and relationships. Structure owns composition and its native/local overlays; the adapter owns no master data.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller owns the required port; a source/product-specific adapter implements it using published owner APIs. The adapter can reference both contract packages; source application does not depend on consumer implementation.

## CON-033

**Name:** Prompt search/version retrieval

**Mode:** query

**Input contract:** Prompt/version reference, consumer/model compatibility filters, scope and pagination.

**Result contract:** Versioned content/metadata, explicit compatibility warnings and available actions.

**Invariants and failure:** Using a final prompt must not silently change a pinned prompt in a run. Context text is not system authority.

**Source ids:** SRC-003; MAP-11

**Current anchor:** IPromptGalleryService; search/import drivers.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-PROMPTS

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; MOD-STRUCTURE; BND-SIMPLECHATS

**Implementation modules:** MOD-PROMPTS

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-PROMPTS. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-034

**Name:** Prompt authoring/curation

**Mode:** command

**Input contract:** Typed draft/version/publication/collection/import intent, expected revision and verified curator/human authority.

**Result contract:** Owner commit/revision and validation/conflict/approval outcomes.

**Invariants and failure:** Mutations remain Prompts-owned; an Agents curator tool must not introduce Agents-to-Prompts EF writes.

**Source ids:** SRC-003; MAP-11

**Current anchor:** IPromptGalleryService and curator adapter.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-PROMPTS

**Caller modules:** MOD-AGENTS; BND-API

**Implementation modules:** MOD-PROMPTS

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-PROMPTS. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-035

**Name:** Resource catalog and promotion

**Mode:** command_and_query

**Input contract:** Resource references/pagination, typed metadata/connector references and an explicit promotion source StorageObjectReference with scope.

**Result contract:** Resource summary/revision, storage/file-scope descriptor and promotion receipt.

**Invariants and failure:** Promotion is not copying to an arbitrary path and grants no broader access. Resources owns binding metadata; Storage owns bytes.

**Source ids:** SRC-014; MAP-11

**Current anchor:** ResourcesService, ResourceStorageObjectPromotionService, source gateway.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-RESOURCES

**Caller modules:** MOD-STRUCTURE; MOD-AGENTS; BND-API

**Implementation modules:** MOD-RESOURCES

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-RESOURCES. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-036

**Name:** TestLab plan/run/evidence

**Mode:** command_and_query

**Input contract:** Plan/case/target-revision references, supported runner intent and authorized evidence handles.

**Result contract:** Run/verdict/evidence references with coverage/limitations; explicitly unsupported runners.

**Invariants and failure:** Do not claim every node can be tested when its kind has no runner. Historical target-revision verdicts do not transfer to new content.

**Source ids:** SRC-016; SRC-004; MAP-11

**Current anchor:** TestLabService; TestPlan/Case/Evidence/Run.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-TESTLAB

**Caller modules:** MOD-STRUCTURE; MOD-PROCESSES; MOD-AGENTS; BND-API

**Implementation modules:** MOD-TESTLAB

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-TESTLAB. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-037

**Name:** Schedule plan and fire dispatch

**Mode:** command_and_query

**Input contract:** Typed target reference/version, normalized inputs, cron/time zone/misfire policy and expected revision; internal firing identity.

**Result contract:** Schedule revision, dispatch receipt/linked runtime RunId and deferred/failed/unavailable states.

**Invariants and failure:** Quartz is not a second plan master. Repeated firings retain the same identity; unsupported process targets must not return fake success.

**Source ids:** SRC-013; SRC-003; MAP-11

**Current anchor:** SchedulerPlannerRunDispatcher and target launchers.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-SCHEDULER

**Caller modules:** MOD-AGENTS; BND-API; BND-WORKFLOWS; MOD-PROCESSES

**Implementation modules:** MOD-SCHEDULER

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-SCHEDULER. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-038

**Name:** Memory operation API

**Mode:** command_and_query

**Input contract:** Operation kind/configuration, bounded query or source reference/snapshot, purpose/scope and source revisions.

**Result contract:** Operation identity/status and retrieval results with provenance/coverage and explicit limitations.

**Invariants and failure:** Retrieval is not authority for source mutations; owner policy filters data. Ingestion idempotency/cancellation follows the specific operation semantics.

**Source ids:** SRC-018; SRC-003; MAP-11

**Current anchor:** src/Memory application contracts and runtime provider.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-MEMORY

**Caller modules:** MOD-AGENTS; MOD-STRUCTURE; MOD-RESOURCES; MOD-CRM; BND-API

**Implementation modules:** MOD-MEMORY

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-MEMORY. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-039

**Name:** Memory source snapshot publication

**Mode:** extension_query

**Input contract:** Owner-specific source reference/version and permitted export shape/purpose; incremental cursor where supported.

**Result contract:** Sanitized immutable source snapshot with access/provenance/removal information.

**Invariants and failure:** Memory owns this consumer port; an integration adapter depends on the source owner's query API. Do not pass DbContext. Revocation must block stale retrieval access.

**Source ids:** MAP-02; MAP-08; MAP-11

**Current anchor:** IMemorySourceGatewayAdapter/source snapshot providers.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-MEMORY

**Caller modules:** MOD-MEMORY

**Implementation modules:** MOD-STRUCTURE; MOD-RESOURCES; MOD-CRM; MOD-PROJECTS

**Extension implementers:** MOD-STRUCTURE; MOD-RESOURCES; MOD-CRM; MOD-PROJECTS

**Data authority:** The source owner owns exported content and access policy. Memory owns its derived index, not the source.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller owns the required port; a source/product-specific adapter implements it using published owner APIs. The adapter can reference both contract packages; source application does not depend on consumer implementation.

## CON-040

**Name:** Plugin lifecycle/capability API

**Mode:** command_and_query

**Input contract:** Install/activate/grant/connect intent with verified actor and manifest/version; use-time capability query.

**Result contract:** Installation/connection references, active/disabled/unavailable states and grant results, without secrets.

**Invariants and failure:** Registry publication does not confer owner-resource access. Disabling or grant changes invalidates leases and requires revalidation.

**Source ids:** SRC-019; MAP-11

**Current anchor:** PluginGrantEvaluator, plugin services and runtime registrars.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-PLUGINS

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; MOD-WORKSPACE; BND-API

**Implementation modules:** MOD-PLUGINS

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-PLUGINS. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-041

**Name:** Connector execution

**Mode:** command_and_query

**Input contract:** Declared connector/operation schema, owner configuration reference, scope, stable delivery identity and bounded payload.

**Result contract:** Capability availability, durable delivery status/receipt where supported, and typed external uncertainty.

**Invariants and failure:** A manifest without a handler is unavailable. Retrying non-idempotent external effects requires reconciliation; an outbox does not imply exactly once.

**Source ids:** MAP-11; SRC-002

**Current anchor:** ConnectorCommandHandler registry/outbox; verify actual handlers.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** BND-CONNECTORS

**Caller modules:** MOD-RESOURCES; MOD-AGENTS; BND-WORKFLOWS; MOD-WORKSPACE

**Implementation modules:** BND-CONNECTORS

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to BND-CONNECTORS. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-042

**Name:** Secret reference and purpose-scoped use

**Mode:** command_and_query

**Input contract:** SecretReference with verified consumer identity/purpose; lifecycle operations include scope/revision.

**Result contract:** Safe metadata or a short-lived use handle; values only inside the authorized adapter.

**Invariants and failure:** No values in DTOs, exports or snapshots. Preserve reference-aware deletion guards; revoked or missing secrets fail closed.

**Source ids:** SRC-009; MAP-11

**Current anchor:** ISecretRuntimeResolver, SecretService, PluginSecretBroker.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-SECURITY

**Caller modules:** MOD-PROVIDERS; MOD-PLUGINS; BND-STORAGE; MOD-WORKSPACE; MOD-AGENTS

**Implementation modules:** MOD-SECURITY

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-SECURITY. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-043

**Name:** Managed storage lifecycle

**Mode:** command_and_query

**Input contract:** Authorized scope, logical object/staging references, expected content revision, bounded stream/name/MIME and explicit disposition.

**Result contract:** Object/revision/hash/locator evidence and finalize/delete/retain receipts; integrity/ownership/conflict errors.

**Invariants and failure:** No arbitrary absolute paths. Define finalization, database receipt and cleanup protocols. Check shared references, active runs and protected roots before deletion.

**Source ids:** SRC-004; SRC-005; SRC-008

**Current anchor:** IStoragePlacementService, managed storage, FileTools binding boundaries.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** BND-STORAGE

**Caller modules:** MOD-STRUCTURE; MOD-RESOURCES; MOD-PROCESSES; BND-WORKFLOWS; MOD-TESTLAB

**Implementation modules:** BND-STORAGE

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to BND-STORAGE. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-044

**Name:** Context surface capture

**Mode:** extension_query

**Input contract:** Surface/session identity, selected project/nodes/view and requested capture purpose.

**Result contract:** Immutable bounded context/snapshot handle with generation, fingerprint, expiry and omissions.

**Invariants and failure:** Carry no component, service or EF objects. Explicitly select stale snapshot reads; revalidate mutations. Simple Chats does not receive this context implicitly.

**Source ids:** SRC-008; SRC-006; SRC-009

**Current anchor:** AgentChatContextSurfaceProvider/builders; proposed Agents-specific application adapter port, not neutral conversation presentation.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-AGENTS

**Caller modules:** MOD-AGENTS

**Implementation modules:** MOD-STRUCTURE; MOD-PROJECTS; MOD-RESOURCES; MOD-PROCESSES

**Extension implementers:** MOD-STRUCTURE; MOD-PROJECTS; MOD-RESOURCES; MOD-PROCESSES

**Data authority:** The source owner supplies content; the Agents application capture adapter owns the immutable invocation attachment. Neutral conversation UI owns neither business context nor grants.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller owns the required port; a source/product-specific adapter implements it using published owner APIs. The adapter can reference both contract packages; source application does not depend on consumer implementation.

## CON-045

**Name:** Post-commit invalidation/live progress

**Mode:** event_notification

**Input contract:** Owner source reference/revision, scope generation, operation correlation and bounded event data.

**Result contract:** Disposable subscription/token and invalidation hint; query the owner for durable state.

**Invariants and failure:** Live events neither grant access nor prove commit. Ignore stale generations; dispose subscriptions on surface/profile changes.

**Source ids:** SRC-002; SRC-009; SRC-006

**Current anchor:** Activity/live channels and module invalidation signals.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** BND-COMPOSITION

**Caller modules:** MOD-STRUCTURE; MOD-AGENTS; MOD-CRM; MOD-PROCESSES; BND-SIMPLECHATS

**Implementation modules:** BND-COMPOSITION

**Extension implementers:** None

**Data authority:** The source module owns event content and meaning. Composition/notification infrastructure owns only transport, subscriptions and lifetime.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller -> owner contract; owner application implements it; composition wires adapters. Neutral conversation UI is not a direct caller; the product-specific adapter invokes the domain contract.

## CON-046

**Name:** Project lifecycle participant protocol

**Mode:** extension_command

**Input contract:** Project lifecycle operation identity, project/source scope, pinned plan/revision, explicit deletion/archival/transfer disposition and verified actor.

**Result contract:** Prepared effects, owner-specific commit/cleanup receipts, compensation availability and blockers.

**Invariants and failure:** The coordinator does not own participant data. Version export schemas. Imports must not implicitly activate secrets or live runs; resumption must not duplicate effects.

**Source ids:** MAP-01; MAP-10; MAP-11

**Current anchor:** Project deletion/transfer participants. Database-profile transfer is the separate CON-056.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-PROJECTS

**Caller modules:** MOD-PROJECTS

**Implementation modules:** MOD-STRUCTURE; MOD-CRM; MOD-RESOURCES; MOD-PROMPTS; MOD-TESTLAB; MOD-PROCESSES

**Extension implementers:** MOD-STRUCTURE; MOD-CRM; MOD-RESOURCES; MOD-PROMPTS; MOD-TESTLAB; MOD-PROCESSES

**Data authority:** Projects owns the project lifecycle and operation plan. Every participant owns changes to its data and its cleanup receipt.

**Registration owner:** BND-COMPOSITION

**Direction note:** Projects application -> Projects lifecycle port -> participant adapter -> owner service. Composition only registers adapters.

## CON-047

**Name:** Workspace preference API

**Mode:** command_and_query

**Input contract:** Workspace identity/scope, typed preference keys/values and expected revision.

**Result contract:** Preference summary/revision with validation of referenced owners.

**Invariants and failure:** Default providers are opaque references; catalog and pricing changes delegate to Providers. A configuration store is not a bag of foreign entities.

**Source ids:** MAP-11; SRC-021

**Current anchor:** WorkspaceService and IWorkspaceProviderCatalog consumer port.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-WORKSPACE

**Caller modules:** MOD-AGENTS; MOD-STRUCTURE; BND-COMPOSITION; BND-API

**Implementation modules:** MOD-WORKSPACE

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-WORKSPACE. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-048

**Name:** Database profile and host capability control

**Mode:** control_plane_command_and_query

**Input contract:** Operator-authorized profile activation/schema/transfer action, expected profile generation and supported host capability.

**Result contract:** New profile-generation/incarnation handle or explicit capability-unavailable result; lifecycle/drain outcome.

**Invariants and failure:** A background worker must not inherit the current UI session's connection. Do not release child-process leases while a run is running or waiting.

**Source ids:** SRC-008; SRC-009; MAP-10

**Current anchor:** DatabaseProfileWorkspaceService/control plane and HostCapabilities.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** BND-COMPOSITION

**Caller modules:** MOD-WORKSPACE; MOD-STRUCTURE; MOD-AGENTS; BND-API

**Implementation modules:** BND-COMPOSITION

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to BND-COMPOSITION. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-049

**Name:** Ordinary Simple Chats operations

**Mode:** command_and_query

**Input contract:** Conversation/definition references, explicit ordinary input, turn OperationId and ETag according to the existing contract.

**Result contract:** Durable admission, replayable stream/status, transcript and retained operations, without agent tools.

**Invariants and failure:** Preserve the non-idempotent HTTP conversation-create baseline until separately changed; turns are retry-safe. Closing/disconnecting a surface does not cancel a run.

**Source ids:** SRC-006; SRC-009

**Current anchor:** LLM Chats product/persistence and Web adapter.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** BND-SIMPLECHATS

**Caller modules:** BND-API; MOD-AGENTS; BND-SIMPLECHATS

**Implementation modules:** BND-SIMPLECHATS

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to BND-SIMPLECHATS. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller -> owner contract; owner application implements it; composition wires adapters. Neutral conversation UI is not a direct caller; the product-specific adapter invokes the domain contract.

## CON-050

**Name:** Collaboration thread/message

**Mode:** command_and_query

**Input contract:** Thread/member/target references, bounded text/attachment handles, owned revision and verified actor.

**Result contract:** Message/thread/inbox states and membership failures.

**Invariants and failure:** Enforce owned membership policy, with no implicit merging into Simple Chats or agent transcripts. Cross-object reads use current authorization.

**Source ids:** MAP-11; SRC-020

**Current anchor:** CollaborationService and owned thread/message records.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-COLLAB

**Caller modules:** MOD-PROJECTS; MOD-STRUCTURE; BND-API

**Implementation modules:** MOD-COLLAB

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to MOD-COLLAB. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-051

**Name:** Execution source authority resolver

**Mode:** extension_query_and_admission

**Input contract:** Trusted source descriptor from its owner, principal/purpose/profile scope, source revision and required operations.

**Result contract:** Verified effective authority with denied/malformed/stale results and a persisted restoration descriptor.

**Invariants and failure:** Source modules own product source providers. No product hardcoding in neutral runtime. Continuation cannot implicitly upgrade legacy ungoverned execution into governed execution.

**Source ids:** SRC-009; SRC-003

**Current anchor:** IAgentExecutionSourceAuthorityProvider registry.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-AGENTS

**Caller modules:** MOD-AGENTS; MOD-PROCESSES; BND-WORKFLOWS

**Implementation modules:** MOD-PROJECTS; MOD-STRUCTURE; MOD-PROCESSES

**Extension implementers:** MOD-PROJECTS; MOD-STRUCTURE; MOD-PROCESSES

**Data authority:** Source owners validate actual origin and scope; execution runtime enforces verified authority and its restoration, rechecking current policy at use time.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller owns the required port; a source/product-specific adapter implements it using published owner APIs. The adapter can reference both contract packages; source application does not depend on consumer implementation.

## CON-052

**Name:** Authorized file browsing/opening

**Mode:** query_and_command

**Input contract:** Owner-issued file collection/object-scope descriptor, source revision, explicit browse/read/write/open action and current actor.

**Result contract:** Bounded file listings/content or write revisions and platform-capability/open receipts.

**Invariants and failure:** Revalidate traversal, symlink and retarget policies; neither browser nor agent may invent a root. Desktop folder opening requires host support.

**Source ids:** SRC-004; SRC-008; SRC-025

**Current anchor:** FileTools scopes, ProjectStructureFileScopeResolver/local opener.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** BND-STORAGE

**Caller modules:** MOD-STRUCTURE; MOD-RESOURCES; MOD-AGENTS; BND-API

**Implementation modules:** BND-STORAGE

**Extension implementers:** None

**Data authority:** Published domain facts and mutations belong to BND-STORAGE. Foreign source references do not transfer authority.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-053

**Name:** Managed curator launch port

**Mode:** consumer_port

**Input contract:** An owner-domain curation goal, source references and scope; resolve managed agent identity through Agents.

**Result contract:** Accepted curator run/session or explicit unavailable result; proposals remain separate from owner mutations.

**Invariants and failure:** Prompts, Workflows and Capability curation may own distinct concrete launch ports; no universal arbitrary administrative-agent command.

**Source ids:** SRC-003; MAP-11

**Current anchor:** IPromptGalleryCuratorLauncher; analogous concrete curator adapters.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-PROMPTS

**Caller modules:** MOD-PROMPTS

**Implementation modules:** MOD-AGENTS

**Extension implementers:** MOD-AGENTS

**Data authority:** Prompts owns the curation request and prompt mutations. Agents owns accepted curator execution; the adapter only translates.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller owns the required port; a source/product-specific adapter implements it using published owner APIs. The adapter can reference both contract packages; source application does not depend on consumer implementation.

## CON-054

**Name:** Agent run result publication

**Mode:** durable_event_and_query

**Input contract:** Owner-specific RunId and source revision/sequence, ArtifactManifest, validated output roles and original destination binding.

**Result contract:** Identified runtime outputs and destination-delivery evidence; acknowledgment through a receipt, not a change to the source result.

**Invariants and failure:** Publication retries do not duplicate nodes/assets. Execution completion and writeback are separate; a new LastRunId cannot appropriate old output. Destination receipts are not a second run store.

**Source ids:** SRC-009; SRC-003; USER-001

**Current anchor:** Owner execution/report adapter; verify the current durable store, events and callers before implementation.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** MOD-AGENTS

**Caller modules:** MOD-STRUCTURE; BND-WORK; MOD-TESTLAB; MOD-RESOURCES

**Implementation modules:** MOD-AGENTS

**Extension implementers:** None

**Data authority:** MOD-AGENTS owns its run, manifest and source revision. Destination owners own their operation receipts and bindings; Work Management owns work acceptance.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-055

**Name:** Workflow run result publication

**Mode:** durable_event_and_query

**Input contract:** Owner-specific RunId and source revision/sequence, ArtifactManifest, validated output roles and original destination binding.

**Result contract:** Identified runtime outputs and destination-delivery evidence; acknowledgment through a receipt, not a change to the source result.

**Invariants and failure:** Publication retries do not duplicate nodes/assets. Execution completion and writeback are separate; a new LastRunId cannot appropriate old output. Destination receipts are not a second run store.

**Source ids:** SRC-026; SRC-027; USER-001

**Current anchor:** Owner execution/report adapter; verify the current durable store, events and callers before implementation.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** BND-WORKFLOWS

**Caller modules:** MOD-STRUCTURE; BND-WORK; MOD-TESTLAB; MOD-RESOURCES

**Implementation modules:** BND-WORKFLOWS

**Extension implementers:** None

**Data authority:** BND-WORKFLOWS owns its run, manifest and source revision. Destination owners own their operation receipts and bindings; Work Management owns work acceptance.

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller → owner contract; owner application implements it. Composition wires concrete adapters.

## CON-056

**Name:** Database profile transfer participant protocol

**Mode:** extension_command

**Input contract:** Operator-authorized source/destination profiles, schema/manifest versions, incarnation, identity-remapping policy, stable transfer identity and owner partition.

**Result contract:** Prepared effects, owner-specific commit/cleanup receipts, compensation availability and blockers.

**Invariants and failure:** The coordinator does not own participant data. Version export schemas. Imports do not implicitly activate secrets or live runs; resumption must not duplicate effects.

**Source ids:** MAP-01; MAP-10; MAP-11

**Current anchor:** Database transfer contributors/control-plane services. Derive participants from affected data, not a mandatory number of modules.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process; an external adapter only where the product publishes an HTTP, tool or plugin boundary.

**Contract owner:** BND-COMPOSITION

**Caller modules:** BND-COMPOSITION

**Implementation modules:** MOD-AGENTS; MOD-PROVIDERS; MOD-CRM; MOD-PROJECTS; MOD-STRUCTURE; MOD-PROCESSES; MOD-PROMPTS; MOD-RESOURCES; MOD-SCHEDULER; MOD-MEMORY; MOD-PLUGINS; MOD-SECURITY; MOD-COLLAB; MOD-WORKSPACE; MOD-TESTLAB; BND-WORK; BND-WORKFLOWS; BND-SIMPLECHATS; BND-STORAGE

**Extension implementers:** MOD-AGENTS; MOD-PROVIDERS; MOD-CRM; MOD-PROJECTS; MOD-STRUCTURE; MOD-PROCESSES; MOD-PROMPTS; MOD-RESOURCES; MOD-SCHEDULER; MOD-MEMORY; MOD-PLUGINS; MOD-SECURITY; MOD-COLLAB; MOD-WORKSPACE; MOD-TESTLAB; BND-WORK; BND-WORKFLOWS; BND-SIMPLECHATS; BND-STORAGE

**Data authority:** Control plane owns transfer plans, profile activation and incarnation. Each participant owns its data; controllers and schema composition must not write foreign tables outside the owner protocol.

**Registration owner:** BND-COMPOSITION

**Direction note:** Control-plane coordinator -> control-plane transfer port -> each owner adapter/service. Distinct from project lifecycle CON-046.

## CON-057

**Name:** Automation-safe CRM search and identity resolution

**Mode:** Query

**Input contract:** Bounded text/record-kind/IDs and requested field purpose; verified caller, scope, and egress policy outside model DTO.

**Result contract:** Safe typed summaries, stable party/resource refs, source revision where supported, redaction, trust, coverage and ambiguity.

**Invariants and failure:** No confidential default details; no global empty-success on outage. Revalidate source eligibility when assigning work. Separate read permission, provider disclosure, and destination disclosure.

**Source ids:** SRC-029; SRC-032

**Current anchor:** Reuse ICrmHrAgentQueryService through an owner-safe boundary; do not duplicate it under Agents.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process typed owner API; separately governed tool, executor, UI, or HTTP adapters where explicitly supported.

**Contract owner:** MOD-CRM

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Implementation modules:** MOD-CRM

**Extension implementers:** None

**Data authority:** MOD-CRM

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller modules identify architectural consumers, not automatic grants or currently attached runtime tools. Owner commands and use-time policy remain authoritative.

## CON-058

**Name:** Bounded CRM party/contact creation

**Mode:** Command

**Input contract:** Allowlisted nonsensitive person/organization/unit fields, creation intent identity and actor scope. Contact-point fields only where current owner supports them.

**Result contract:** Canonical Party reference, owner revision, committed receipt and explicit validation/duplicate/reconciliation result.

**Invariants and failure:** Reuse canonical CRM command. Creating Party is not proof all contact-point variants are supported. Duplicate suggestion is not an automatic merge. Preserve approvals, audit and sensitivity policy.

**Source ids:** SRC-029

**Current anchor:** ICrmPartyCommandService.CreatePartyAsync is already wired by HR; preserve this path.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process typed owner API; separately governed tool, executor, UI, or HTTP adapters where explicitly supported.

**Contract owner:** MOD-CRM

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Implementation modules:** MOD-CRM

**Extension implementers:** None

**Data authority:** MOD-CRM

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller modules identify architectural consumers, not automatic grants or currently attached runtime tools. Owner commands and use-time policy remain authoritative.

## CON-059

**Name:** CRM affiliation read and mutation

**Mode:** Query / Command

**Input contract:** Person and organization references, bounded affiliation patch, expected relevant revision and command identity.

**Result contract:** Safe affiliations or committed affiliation result with preserved restricted fields.

**Invariants and failure:** CRM validates endpoints and role/cardinality/interval; no arbitrary workforce/confidential-field update through affiliation.

**Source ids:** SRC-029

**Current anchor:** ListAffiliationsAsync and UpsertAffiliationAsync are wired by HR.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process typed owner API; separately governed tool, executor, UI, or HTTP adapters where explicitly supported.

**Contract owner:** MOD-CRM

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Implementation modules:** MOD-CRM

**Extension implementers:** None

**Data authority:** MOD-CRM

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller modules identify architectural consumers, not automatic grants or currently attached runtime tools. Owner commands and use-time policy remain authoritative.

## CON-060

**Name:** Staffing/resource-profile administration

**Mode:** Query / Command

**Input contract:** CRM resource/workforce identity, allowlisted local role/availability/skills/capacity patch and expected revision.

**Result contract:** Owner-validated local staffing result and safe source references; reservations remain CON-013.

**Invariants and failure:** Read and edit grants are separate. No technical agent configuration, AI-price rewrite, or task assignment through CRM-owned fields. New workforce automation surfaces require explicit implementation/approval.

**Source ids:** USER-002; SRC-032; MAP-08

**Current anchor:** Actual Workforce/resource service and supported mutations must be rebaselined; no general existing HR staffing-write tool is claimed.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process typed owner API; separately governed tool, executor, UI, or HTTP adapters where explicitly supported.

**Contract owner:** MOD-CRM

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Implementation modules:** MOD-CRM

**Extension implementers:** None

**Data authority:** MOD-CRM

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller modules identify architectural consumers, not automatic grants or currently attached runtime tools. Owner commands and use-time policy remain authoritative.

## CON-061

**Name:** Simple Chats definition discovery for administrators

**Mode:** Query

**Input contract:** Scoped search, status, tags, paging, or exact definition ID; safe field profile.

**Result contract:** Definition summary or permitted editable settings, revision and expected concurrency token.

**Invariants and failure:** Definition visibility does not grant access to conversations/transcripts, secrets, deployments, or retention. No inferred project context.

**Source ids:** SRC-033; SRC-034; USER-002

**Current anchor:** Reuse ILlmChatDefinitionApplicationService.Get/List/ListPage; external administration adapter is new.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process typed owner API; separately governed tool, executor, UI, or HTTP adapters where explicitly supported.

**Contract owner:** BND-SIMPLECHATS

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Implementation modules:** BND-SIMPLECHATS

**Extension implementers:** None

**Data authority:** BND-SIMPLECHATS

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller modules identify architectural consumers, not automatic grants or currently attached runtime tools. Owner commands and use-time policy remain authoritative.

## CON-062

**Name:** Simple Chats definition administration

**Mode:** Command

**Input contract:** Typed create/update/status intent with allowlisted definition fields, expected token for changes, revision reason and stable command identity for new retry-safe automation.

**Result contract:** Canonical definition/revision/token plus truthful owner receipt or conflict/rejection/unconfirmed status.

**Invariants and failure:** Call existing owner service, not EF/editor state. Preserve ordinary execution and pinned/admitted revisions. New durable create dedupe must commit atomically with definition; a post-hoc wrapper is insufficient. No transcript or turn manipulation.

**Source ids:** SRC-033; SRC-034; USER-002

**Current anchor:** Existing CreateAsync/UpdateAsync/ChangeStatusAsync; no HR tool wiring observed, and current request shapes do not promise retry-safe creation.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process typed owner API; separately governed tool, executor, UI, or HTTP adapters where explicitly supported.

**Contract owner:** BND-SIMPLECHATS

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Implementation modules:** BND-SIMPLECHATS

**Extension implementers:** None

**Data authority:** BND-SIMPLECHATS

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller modules identify architectural consumers, not automatic grants or currently attached runtime tools. Owner commands and use-time policy remain authoritative.

## CON-063

**Name:** Managed agent administration adapter

**Mode:** Query / Command

**Input contract:** Safe catalog/settings/options or allowed create/update/avatar/review intent with verified managed actor and exact target.

**Result contract:** Technical owner result, revisions, safe readiness warnings and separate downstream effect status.

**Invariants and failure:** Reuse existing HR service/CON-002; deny self/privileged grants and protected target changes according to current policy. Editing another definition never widens the running actor authority. No raw provider credentials.

**Source ids:** SRC-029; SRC-030

**Current anchor:** HrAgentAdministrationService and HrAgentRuntimeToolProvider; currently interactive managed-HR surface.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process typed owner API; separately governed tool, executor, UI, or HTTP adapters where explicitly supported.

**Contract owner:** MOD-AGENTS

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Implementation modules:** MOD-AGENTS

**Extension implementers:** None

**Data authority:** MOD-AGENTS

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller modules identify architectural consumers, not automatic grants or currently attached runtime tools. Owner commands and use-time policy remain authoritative.

## CON-064

**Name:** Workflow definition and component curation

**Mode:** Query / Command

**Input contract:** Definition/version/component refs, bounded validated patch, expected revision, publication intent and approval.

**Result contract:** New draft/revision or explicitly approved lifecycle result with validation diagnostics.

**Invariants and failure:** Definition edits do not alter admitted runs/checkpoints. Capability/schema validation and exact publication permission; no general process persistence.

**Source ids:** SRC-028; USER-002

**Current anchor:** WorkflowCurator runtime provider is documented; inspect actual methods/registration before changing it.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process typed owner API; separately governed tool, executor, UI, or HTTP adapters where explicitly supported.

**Contract owner:** BND-WORKFLOWS

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Implementation modules:** BND-WORKFLOWS

**Extension implementers:** None

**Data authority:** BND-WORKFLOWS

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller modules identify architectural consumers, not automatic grants or currently attached runtime tools. Owner commands and use-time policy remain authoritative.

## CON-065

**Name:** Capability catalog curation

**Mode:** Query / Command

**Input contract:** Bounded capability metadata/definition patch and target revision, separate policy authority.

**Result contract:** Validated capability draft/revision and safe diagnostics, not a grant to execute.

**Invariants and failure:** Catalog curation is distinct from permission assignment/secret access. No arbitrary code or privileged self-grant via schema. Reuse registered curator, verify exact surface.

**Source ids:** SRC-028

**Current anchor:** CapabilityCurator runtime provider is documented; current complete method inventory not reread.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process typed owner API; separately governed tool, executor, UI, or HTTP adapters where explicitly supported.

**Contract owner:** MOD-AGENTS

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Implementation modules:** MOD-AGENTS

**Extension implementers:** None

**Data authority:** MOD-AGENTS

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller modules identify architectural consumers, not automatic grants or currently attached runtime tools. Owner commands and use-time policy remain authoritative.

## CON-066

**Name:** Scheduler target and schedule discovery

**Mode:** Query

**Input contract:** Bounded search, target kind, scope and allowed field projection.

**Result contract:** Available exact workflow/version targets and safe schedule summaries with coverage.

**Invariants and failure:** Do not return saved input JSON by default. Offered targets require actual adapter availability; current managed provider exposes workflow targets only.

**Source ids:** SRC-031

**Current anchor:** SchedulerWorkflowTargetsSearch and SchedulerWorkflowSchedulesSearch.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process typed owner API; separately governed tool, executor, UI, or HTTP adapters where explicitly supported.

**Contract owner:** MOD-SCHEDULER

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Implementation modules:** MOD-SCHEDULER

**Extension implementers:** None

**Data authority:** MOD-SCHEDULER

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller modules identify architectural consumers, not automatic grants or currently attached runtime tools. Owner commands and use-time policy remain authoritative.

## CON-067

**Name:** Governed workflow schedule administration

**Mode:** Command

**Input contract:** Discovered workflow/version, typed schedule/timezone/misfire/bounded input and enable/start/end fields; intent and change revision.

**Result contract:** Canonical plan and next-fire metadata with receipt; later dispatch has a distinct run receipt.

**Invariants and failure:** Existing create path retained; update/disable/delete as automation are target extensions only if approved. Check execution authority at each firing; process scheduling is not implicitly enabled.

**Source ids:** SRC-031; USER-002

**Current anchor:** Current managed tool creates workflow schedules through ISchedulerPlannerService.SavePlanAsync.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process typed owner API; separately governed tool, executor, UI, or HTTP adapters where explicitly supported.

**Contract owner:** MOD-SCHEDULER

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Implementation modules:** MOD-SCHEDULER

**Extension implementers:** None

**Data authority:** MOD-SCHEDULER

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller modules identify architectural consumers, not automatic grants or currently attached runtime tools. Owner commands and use-time policy remain authoritative.

## CON-068

**Name:** Process definition and planning administration

**Mode:** Query / Command

**Input contract:** Typed draft/definition/assignment edit with source revision, permitted purpose and scope.

**Result contract:** Process-owned definition/revision or explicit invalid/unsupported result.

**Invariants and failure:** UI/HTTP existence does not attach runtime tools. Definition editing, run control, task planning, and execution assignments remain distinct. A process agent needs a reviewed adapter, not direct runtime-store writes.

**Source ids:** SRC-028; SRC-035; USER-002

**Current anchor:** Rebaseline existing process application methods; no general first-party Process tool provider is currently documented.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process typed owner API; separately governed tool, executor, UI, or HTTP adapters where explicitly supported.

**Contract owner:** MOD-PROCESSES

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Implementation modules:** MOD-PROCESSES

**Extension implementers:** None

**Data authority:** MOD-PROCESSES

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller modules identify architectural consumers, not automatic grants or currently attached runtime tools. Owner commands and use-time policy remain authoritative.

## CON-069

**Name:** Authorized Resources discovery and retrieval metadata

**Mode:** Query

**Input contract:** Resource IDs/kinds/search, bounded fields, source scope and intended data use.

**Result contract:** Catalog summaries, owner-issued references/version/content handles and explicit availability/coverage.

**Invariants and failure:** Distinguish catalog Resource from CRM staffing resource. No raw Storage locator/credential or implied grant from a display label. Resolve current access at content use.

**Source ids:** SRC-036; MAP-11; USER-002

**Current anchor:** Resources owner services; exact current runtime attachment requires discovery.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process typed owner API; separately governed tool, executor, UI, or HTTP adapters where explicitly supported.

**Contract owner:** MOD-RESOURCES

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Implementation modules:** MOD-RESOURCES

**Extension implementers:** None

**Data authority:** MOD-RESOURCES

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller modules identify architectural consumers, not automatic grants or currently attached runtime tools. Owner commands and use-time policy remain authoritative.

## CON-070

**Name:** Resource catalog creation and metadata administration

**Mode:** Command

**Input contract:** Bounded resource metadata, source/storage reference, supported kind/schema, expected revision and command identity.

**Result contract:** Canonical Resource with provenance, revision, binding/content state and effect receipt.

**Invariants and failure:** Resources owns catalog metadata, Storage bytes, CRM staffing facts. Promotion/creation is explicit; content replacement uses Storage policy. No arbitrary connector provisioning through a Resource patch.

**Source ids:** SRC-036; MAP-11; USER-002

**Current anchor:** Reuse ResourcesService/promotion paths after current-code discovery; do not claim every CRUD action is an attached agent tool.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process typed owner API; separately governed tool, executor, UI, or HTTP adapters where explicitly supported.

**Contract owner:** MOD-RESOURCES

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Implementation modules:** MOD-RESOURCES

**Extension implementers:** None

**Data authority:** MOD-RESOURCES

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller modules identify architectural consumers, not automatic grants or currently attached runtime tools. Owner commands and use-time policy remain authoritative.

## CON-071

**Name:** Authorized content streaming for automation

**Mode:** Query / Content access

**Input contract:** Owner-issued content/version reference, bounded range/size/format and resolved use-time scope/egress permit.

**Result contract:** Safe bytes/stream or bounded excerpt with version/hash/coverage and redaction where applicable.

**Invariants and failure:** Read-only may still disclose sensitive data. Recheck root/ACL/alias/symlink before I/O; no arbitrary URL fetch or secret disclosure. Do not materialize complete documents into broad search responses.

**Source ids:** SRC-008; SRC-033; USER-002

**Current anchor:** Reuse authorized FileTools/storage access through domain adapters, not an unrestricted file/HTTP gateway.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process typed owner API; separately governed tool, executor, UI, or HTTP adapters where explicitly supported.

**Contract owner:** BND-STORAGE

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Implementation modules:** BND-STORAGE

**Extension implementers:** None

**Data authority:** BND-STORAGE

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller modules identify architectural consumers, not automatic grants or currently attached runtime tools. Owner commands and use-time policy remain authoritative.

## CON-072

**Name:** Workflow executor owner-operation extension port

**Mode:** Extension port

**Input contract:** Registered executor operation schema and version, persisted step/iteration intent ID, verified run delegation and pinned source refs.

**Result contract:** Typed query or owner receipt recorded as step result; required-effect delivery state and recovery reference.

**Invariants and failure:** Executor invokes typed owner API, not self HTTP, fabricated tool call, or foreign EF. No automatic reuse of interactive HR/Scheduler authority. Replay reconciles before a new effect.

**Source ids:** SRC-027; SRC-028; USER-002

**Current anchor:** Existing Structure workflow executor is a concrete precedent; other domain executors are target-specific additions.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process typed owner API; separately governed tool, executor, UI, or HTTP adapters where explicitly supported.

**Contract owner:** BND-WORKFLOWS

**Caller modules:** BND-WORKFLOWS

**Implementation modules:** BND-WORKFLOWS

**Extension implementers:** BND-WORKFLOWS

**Data authority:** Each target module owns its own domain effects; the execution owner owns step identity, checkpoints, and recovery state.

**Registration owner:** BND-COMPOSITION

**Direction note:** Consumer owns executor/step port and its integration adapter. Adapter references target published API. Composition registers it. Destination owns all target data and has no reverse runtime implementation dependency.

**Delegated data owners:** MOD-CRM; BND-SIMPLECHATS; MOD-RESOURCES; MOD-SCHEDULER; MOD-AGENTS; MOD-STRUCTURE; MOD-PROMPTS; MOD-TESTLAB

**Adapter location rule:** Implementation means a separately placed runtime-to-domain integration adapter associated with the consumer boundary, not the target product or its persistence. In particular Simple Chats must never implement a Workflow/Process/Tooling interface inside its product/persistence projects.

## CON-073

**Name:** Process step owner-operation extension port

**Mode:** Extension port

**Input contract:** Registered step operation/version, durable step/iteration intent, trusted process actor/delegation and source/target scope.

**Result contract:** Owner query/result receipt and process-owned checkpoint/effect status.

**Invariants and failure:** No process writer for foreign data. Resolve authority at dispatch/resume; no borrowing a privileged interactive identity. Owner commit before checkpoint loss reconciles using the same intent.

**Source ids:** SRC-028; SRC-035; USER-002

**Current anchor:** Existing process execution adapters retained; domain-operation step adapters require explicit registration/testing.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process typed owner API; separately governed tool, executor, UI, or HTTP adapters where explicitly supported.

**Contract owner:** MOD-PROCESSES

**Caller modules:** MOD-PROCESSES

**Implementation modules:** MOD-PROCESSES

**Extension implementers:** MOD-PROCESSES

**Data authority:** Each target module owns its own domain effects; the execution owner owns step identity, checkpoints, and recovery state.

**Registration owner:** BND-COMPOSITION

**Direction note:** Consumer owns executor/step port and its integration adapter. Adapter references target published API. Composition registers it. Destination owns all target data and has no reverse runtime implementation dependency.

**Delegated data owners:** MOD-CRM; BND-SIMPLECHATS; MOD-RESOURCES; MOD-SCHEDULER; MOD-AGENTS; MOD-STRUCTURE; MOD-PROMPTS; MOD-TESTLAB

**Adapter location rule:** Implementation means a separately placed runtime-to-domain integration adapter associated with the consumer boundary, not the target product or its persistence. In particular Simple Chats must never implement a Workflow/Process/Tooling interface inside its product/persistence projects.

## CON-074

**Name:** Collaboration message contributions

**Mode:** Command

**Input contract:** Authorized thread/object ref, bounded message/attachment references, explicit publication intent and durable command ID.

**Result contract:** Canonical Collaboration message ID and delivery/commit status.

**Invariants and failure:** Membership and destination audience apply; retrieved confidential data cannot be laundered into a broad thread. Not an agent/Simple Chats transcript write.

**Source ids:** MAP-11; USER-002

**Current anchor:** Current Collaboration services must be discovered before exposing automation writes.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process typed owner API; separately governed tool, executor, UI, or HTTP adapters where explicitly supported.

**Contract owner:** MOD-COLLAB

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Implementation modules:** MOD-COLLAB

**Extension implementers:** None

**Data authority:** MOD-COLLAB

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller modules identify architectural consumers, not automatic grants or currently attached runtime tools. Owner commands and use-time policy remain authoritative.

## CON-075

**Name:** Memory ingestion of owner-published evidence

**Mode:** Command / Operation query

**Input contract:** Authorized source snapshot/reference, provenance, purpose, bounded payload and idempotent ingestion identity.

**Result contract:** Ingestion operation/status, source revision and completeness, not a source-master update.

**Invariants and failure:** Derived memory remains revocable and nonauthoritative. Agent claims do not become verified business facts; query/command separation and retention apply.

**Source ids:** SRC-028; USER-002

**Current anchor:** Reuse Memory operations/source gateway; exact write-tool surface must be verified.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process typed owner API; separately governed tool, executor, UI, or HTTP adapters where explicitly supported.

**Contract owner:** MOD-MEMORY

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Implementation modules:** MOD-MEMORY

**Extension implementers:** None

**Data authority:** MOD-MEMORY

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller modules identify architectural consumers, not automatic grants or currently attached runtime tools. Owner commands and use-time policy remain authoritative.

## CON-076

**Name:** TestLab evidence and verdict authoring

**Mode:** Command

**Input contract:** Plan/case/target-version refs, authorized evidence content, supported runner output or explicit human verdict and revision.

**Result contract:** Owner evidence/verdict identity and traceable target/content version.

**Invariants and failure:** A model statement is not successful test execution. Distinguish adding evidence from granting acceptance; no fabricated runner PASS.

**Source ids:** MAP-11; USER-002

**Current anchor:** Existing TestLabService and supported runners require rebaseline; new automation grants are explicit.

**Status:** TARGET_SEMANTICS_NOT_IMPLEMENTATION

**Transport:** In-process typed owner API; separately governed tool, executor, UI, or HTTP adapters where explicitly supported.

**Contract owner:** MOD-TESTLAB

**Caller modules:** MOD-AGENTS; BND-WORKFLOWS; MOD-PROCESSES

**Implementation modules:** MOD-TESTLAB

**Extension implementers:** None

**Data authority:** MOD-TESTLAB

**Registration owner:** BND-COMPOSITION

**Direction note:** Caller modules identify architectural consumers, not automatic grants or currently attached runtime tools. Owner commands and use-time policy remain authoritative.
