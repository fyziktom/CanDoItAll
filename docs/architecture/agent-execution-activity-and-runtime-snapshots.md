# Agent Execution Activity And Runtime Snapshots

Status: implemented backend and Blazor feedback contract, 2026-07-27.

This document defines the current contract for visible agent-startup progress,
prepared execution data, and module-owned runtime snapshots. It also defines what
remains canonical and what must never be treated as durable truth.

The central rule is:

> Publish fast, typed, operation-scoped feedback from immutable data already held by
> the application, but revalidate canonical identity and authorization at every
> execution or mutation boundary.

## Ownership And Source Of Truth

| Concern | Current owner | Source-of-truth rule |
| --- | --- | --- |
| Activity types and operation identity | `CanDoItAll.AgentFramework.Models` | Typed feedback contract; not durable run state |
| Activity transition and stream coordination | `CanDoItAll.AgentFramework.Core` | One bounded in-memory partition per operation |
| Generic sequenced stream primitive | `CanDoItAll.SharedKernel.Streaming` | Process-local delivery only |
| Current-profile activity access | `CanDoItAll.Modules.AgentFramework` | Current database profile, generation, and organization scope must match |
| Activity presentation | `CanDoItAll.AgentFramework.Components` | Typed phase mapping; message text is display detail, not a state protocol |
| Agent preparation blueprint | `CanDoItAll.AgentFramework.Core` | Immutable, versioned projection that must be current at use time |
| Canonical provider runtime snapshot | `CanDoItAll.Modules.AgentFramework` | Database provider rows and their concurrency revisions remain canonical |
| Project Structure invocation snapshot | `CanDoItAll.Modules.Workbench` | Bounded copy of the ready UI surface for one invocation |
| Processes invocation snapshot | `CanDoItAll.Modules.Processes` | Bounded copy of the ready projection shell with per-component provenance |
| Chat and execution-run truth | Agent workspace persistence | Durable transcript, run state, approvals, receipts, artifacts, logs, and metrics |

The activity stream is intentionally ephemeral. `ExecutionRunRecord` and
`ChatSessionRecord` remain the durable truth. `ExecutionRunRecord.InitialActivityOperationId`
persists correlation to the operation that started a run, but it does not make the
in-memory activity partition durable.

The split file workspace store uses typed pending commit journals for chat-backed run
creation, generic run creation, and existing-run updates. Those journals recover
canonical workspace projections after an interrupted multi-file commit. They do not
turn activity feedback into an audit log. The recovery tests cover injected
commit-stage interruption; this contract does not claim storage-device flush or
power-loss durability beyond the guarantees of the configured file system.

`ExecutionUpdated` remains a compatibility notification over persisted
`ExecutionLogEntry` values. `IsolatedCompatibilityEventDispatcher<TEvent>` gives each
subscriber a bounded mailbox, runs handlers outside the publisher, and reports handler
failure or overflow without changing canonical execution. It is not the startup
activity stream and should not become a new string-topic event bus.

## Immediate Operation Admission

`AgentChatExecutionOrchestrator.StartSendMessage` and
`StartApprovalContinuation` admit an operation before awaiting context capture,
preparation, provider work, or model execution. Admission synchronously publishes the
first `Accepted` activity and returns an `AgentChatOperationHandle` containing:

- the exact `AgentExecutionActivityStreamId`;
- the eventual `Task<AgentChatRunResult>`.

The UI assigns the stream ID immediately and reads feedback while the completion task is
still running. The older task-returning methods remain compatibility wrappers over the
start methods.

An activity partition is identified by all four values:

1. database profile ID;
2. workspace scope;
3. database profile generation;
4. `AgentExecutionOperationId`.

Agent, chat-session, context, and execution-run identities are bound to the operation as
they become available. They are activity payload fields, not substitutes for the
partition key. A duplicate active/terminal operation, a previously evicted identity, or
exhausted active capacity is rejected explicitly.

`CurrentProfileAgentExecutionActivityReader` authorizes the profile ID, profile
generation, and organization workspace scope before opening a reader, checks them again
after establishing the profile-change lifetime, and cancels that reader when the
database profile lifetime changes.

## Typed Activity Contract

`AgentExecutionActivityPhase` is the state protocol. `AgentExecutionActivity.Message`
is bounded display detail and must never be parsed to determine state.

| Phase | Meaning |
| --- | --- |
| `Accepted` | The operation partition exists and can be observed. |
| `CapturingContext` | The current immutable module/workspace context is being captured. |
| `ResolvingSession` | A chat session or pending-approval run is being resolved. |
| `PreparingInput` | Prompt, transient context, and attachments are being prepared. |
| `ResolvingPreparation` | Versioned agent/provider/capability/memory preparation is being reused or refreshed. |
| `ResolvingProvider` | The current provider and execution policy are being applied. |
| `CreatingExecution` | Durable run admission or creation is in progress. |
| `PreparingCapabilities` | Tools, skills, memory, and other capability inputs are being composed. |
| `PreparingRuntime` | The per-turn runtime is being composed. |
| `WaitingForProvider` | The invocation is waiting on provider work. |
| `Streaming` | The agent is producing a response. |
| `UsingTool` | Runtime execution is waiting on or invoking a tool. |
| `AwaitingApproval` | Tool approval is required. |
| `PersistingResult` | Canonical run/session output is being persisted. |
| `Completed` | Terminal success. |
| `Failed` | Terminal failure. |
| `Cancelled` | Terminal cancellation. |

Transition rules are enforced by `AgentExecutionActivityTransitionRules`; publishers
cannot emit arbitrary phase order. Repeated progress at the same phase is valid. The
runtime permits only its reviewed cycles, including streaming/tool/provider-runtime
progress and approval/persistence continuation paths.

Terminal outcomes have a strict phase mapping:

| Terminal outcome | Required terminal phase |
| --- | --- |
| `Succeeded` | `Completed` |
| `Failed` | `Failed` |
| `Cancelled` | `Cancelled` |
| `Suspended` | `AwaitingApproval` |

`Suspended` is a terminal outcome for the current operation, not a successful or failed
run. The durable run remains waiting on approval. An approval decision starts a new
operation with a new operation ID and activity stream; it does not reopen the completed
partition.

Only failed activity may carry an error code. Activity messages are limited to 2,048
characters and error codes to 128 characters. The shared Blazor component additionally
normalizes and bounds displayed messages to 240 characters. Publishers must still use
sanitized messages; size limits are not secret-redaction policy.

## Bounded Sequenced Delivery

`PartitionedSequencedStream<TKey, TEvent>` provides ordered, process-local delivery.
The default activity policy is:

- 1,024 total partitions;
- 256 retained events per partition;
- 256 retained terminal partitions;
- 10-minute terminal retention;
- 1,024 eviction tombstones;
- 15-minute tombstone retention.

Active partitions are not evicted to admit another active operation. When total
capacity is full, completed partitions are evicted oldest-first; if no completed
partition can make space, admission returns `CapacityExhausted`.

Readers handle a closed set of typed results:

| Read result | Meaning | Consumer behavior |
| --- | --- | --- |
| `SequencedStreamEvents<T>` | One or more ordered events are available. | Apply the events and advance after the last sequence. |
| `SequencedStreamGap<T>` | The requested sequence is older than the retained event window. | Record that updates were skipped and continue from `AvailableFromInclusive`. |
| `SequencedStreamCompleted<T>` | The terminal event was already consumed and no more events exist. | Stop reading. |
| `SequencedStreamEvicted<T>` | A known partition was evicted for retention or capacity. | Stop and report live feedback unavailable. |
| `SequencedStreamUnknown<T>` | No active partition or retained tombstone matches. | Stop and report live feedback unavailable. |

The Blazor `AgentExecutionActivityStatus` component renders typed labels, exposes a
polite `role="status"` region, marks a sequence gap as “Earlier updates skipped,” fences
late reader updates by generation, and distinguishes profile-change cancellation from
an unknown/evicted stream.

## Preparation And Provider Lifetimes

Three different mechanisms must not be collapsed into a single “preloaded agent”
concept.

### Floating-chat metadata pool

`AgentChatPreparationPool` is circuit-scoped. It retains only bounded active
`AgentDefinition` metadata for catalog/start-chat responsiveness. It serializes refresh,
tracks adaptive usage, applies idle retention, and clears entries when reference data is
invalidated. It does not retain a MAF runtime, tool delegate, MCP/A2A client, attachment,
credential, provider response, or conversation session.

### Execution preparation cache

`AgentExecutionPreparationCache` is scoped to the current service graph and has a
default capacity of 64 entries. Its key is:

- database profile ID;
- workspace scope;
- agent ID.

Its required version is:

- persisted catalog revision;
- database profile generation;
- provider configuration fingerprint.

The cache single-flights concurrent creation for the same key/version, uses immutable
copies of the agent, provider, capability, and memory data, evicts only completed
least-recently-used entries, and rejects capacity when all entries are loading.
Supersession, explicit invalidation, database-profile change, and scope disposal cancel
in-flight entries.

`AgentExecutionPreparationService` validates catalog coherence, provider binding,
provider fingerprint, and database profile generation. Atomic run admission validates
the acquired blueprint against the catalog snapshot returned by the same durable
admission boundary. A stale blueprint fails explicitly and reviewed continuation paths
retry with refreshed preparation; it is never silently used.

### Canonical provider snapshot

`CanonicalProviderRuntimeProfileSnapshotService` is singleton because it is an immutable
process-level projection, not user/circuit state. It is initialized after the active
database profile is made ready. Every published state carries database profile ID,
database fingerprint, profile generation, and publication generation.

Provider rows are loaded with `AsNoTracking`. Persistent providers carry their database
`ConcurrencyToken` as `ProviderConfigurationRevision`. A use-time revision probe returns
the existing immutable lease when current, refreshes one provider when changed, and
fails closed by faulting the snapshot when revision verification cannot be trusted.
Provider save/delete commit observers update the projection after the canonical commit;
projection failure is explicit and does not reverse the database commit.

Database-profile changes replace the snapshot with `NotReady`; stale refresh
publication is rejected by profile identity and publication-generation checks.

Provider snapshots and preparation blueprints contain provider configuration, not
resolved secret values. Credentials are resolved asynchronously into a one-use
per-dispatch scope, checked against the same provider fingerprint, and cleared on
disposal. A credential scope must never be promoted into either preparation cache.

Pooling `RuntimeBuildResult` or `AIAgent` remains forbidden. Runtime builds contain
turn-specific context, session, tools, policies, clients, and disposables and are
created for each execution.

## Module Runtime Snapshots

Runtime snapshots are typed invocation attachments carried by
`AgentRuntimeTransientContext`. They are immutable copies of data the active module has
already loaded. They are not caches, domain aggregates, authorization grants, or
write-back sources.

Every attachment envelope carries:

- scope, source, workspace scope, contributor, kind, and publication revision;
- content and coverage fingerprints;
- database profile generation and freshness fingerprint;
- capture time and optional freshness deadline;
- an exact typed payload available only in process memory.

The generic freshness result is `Current`, `Expired`, or `ProfileMismatch`.

### Project Structure

`ProjectStructureAgentChatContextProvider` captures
`ProjectStructureInvocationSnapshot` only from a ready, matching project surface
(excluding the Calendar view). The mapper copies a bounded projection:

- at most 512 nodes and 1,024 links;
- hierarchy, classification, status, progress, priority, schedule, project
  relationships, links, and selection;
- explicit coverage flags/counts and explicit omissions;
- a five-minute freshness lifetime.

Notes, metadata, assets, layout, routes, action capabilities, storage references, and
file contents are intentionally omitted.

`ProjectStructureReadRequest.Source` controls `project_structure_read`:

| Value | Behavior |
| --- | --- |
| `ContextDefault` | Uses `InvocationSnapshot` only for eligible interactive Project Structure chat; otherwise uses `CanonicalCurrent`. |
| `InvocationSnapshot` | Uses the exact held surface snapshot and fails closed on context, scope, project, profile generation, freshness, type, fingerprint, or coverage mismatch. It is rejected outside eligible interactive Project Structure chat. |
| `CanonicalCurrent` | Performs the canonical service read explicitly. |

There is no silent snapshot-to-database fallback. If the snapshot cannot answer a
request, the tool returns a typed `409` failure that tells the caller to request
`CanonicalCurrent`. The response includes its effective `ProjectStructureReadSource`.
Governed process automation always resolves the default to `CanonicalCurrent`.

### Processes

`ProcessInvocationSnapshot` is captured from an already-ready
`ProcessWorkspaceShellProjection` used by the Process workspace or live dashboard. It
copies selection, selected definition, runs, selected-run detail, history, active
agents, usage, and projection provenance, with these bounds:

- at most 32 runs;
- at most 6 recent events per run;
- at most 32 active agents;
- a prompt projection of at most 2 runs, 1 recent event, and 1 active agent.

Each provenance component records `NotRequested`, `Absent`, or `Present`; its source;
absence reason; optional content fingerprint; projection freshness; and optional durable
run-record revision. Snapshot fields are populated only when their corresponding
provenance component is `Present`. Restricted summaries are sanitized/redacted, and
coverage records source/captured counts and redaction count.

The snapshot is published only when the shell refresh is `Ready` and its source
observation is valid. Its deadline is the earlier source-based rule implemented as
`Refresh.ObservedAtUtc + 5 minutes`, not a fresh five minutes from every UI copy. The
component schedules publication reevaluation at expiry. That reevaluation removes the
expired attachment unless the shell has since received a newer valid source
observation; it does not grant permission to query or mutate process storage.

There is currently no direct `processes_*` runtime tool provider. Deeper process reads
remain projection/application/API operations, and writes remain canonical process
commands.

## Safe Concurrency Rules

- Capture complete value snapshots synchronously from one ready UI/projection state;
  never retain a mutable module entity.
- Copy collections into immutable arrays before publication.
- Replace prepared/provider snapshot state atomically; do not mutate a published object
  graph in place.
- Fence asynchronous UI loads and reader pumps with generations so a late result cannot
  overwrite a newer route, selection, profile, or operation.
- Include database profile generation in operation and attachment identity.
- Include source freshness and coverage in module snapshots; a timestamp alone is not
  enough.
- Revalidate canonical authorization and concurrency at tool/mutation boundaries.
- Fail on stale, ambiguous, partial, mismatched, or unavailable state. Do not silently
  substitute a different agent, project, run, provider, or data source.
- Parallelize independent read-only preparation only when every result is joined before
  use and validated against one coherent revision. Writes remain behind their owning
  atomic/optimistic concurrency boundary.

## Extension Checklist

For a module that wants to contribute immediate runtime data:

1. Define a typed `IAgentChatContextAttachment`; do not publish a property bag.
2. Define stable source/scope identity, field coverage, omissions, size bounds, and
   freshness policy.
3. Capture only from a ready module-owned runtime/projection value.
4. Copy primitives and immutable collections; never retain a component, DbContext,
   tracked entity, service provider, callback, or mutable domain object.
5. Compute deterministic content, coverage, and freshness fingerprints.
6. Publish through the context registry with a disposable module lifetime.
7. Define an explicit source-selection policy. If canonical fallback changes semantics,
   require an explicit source value and fail closed.
8. Treat the snapshot as read context only. Reauthorize and reload canonical state for
   mutation.
9. Report work through existing generic activity phases and sanitized messages; add a
   new phase only when it is a cross-module execution state with transition tests.
10. Test bounds, deterministic fingerprints, profile mismatch, expiry, incomplete
    coverage, mismatched scope, late async results, and zero canonical reads on the
    eligible snapshot path.

For a new activity producer:

1. Admit an operation and return its handle before the first asynchronous wait.
2. Bind agent/session/context/run identities exactly once.
3. Follow the transition rules and always terminalize or dispose the lease.
4. Preserve durable run truth before publishing success.
5. Add coordinator, transition, UI, profile-access, and failure-isolation tests.

## HTTP/SSE Projection Status

No HTTP or Server-Sent Events endpoint currently exposes
`AgentExecutionActivity`. Existing agent HTTP commands await their result and create
operation IDs internally. A persisted `ExecutionRunRecord` can expose
`InitialActivityOperationId` for correlation, but the API does not return an
authorized activity-stream handle for later subscription.

A future SSE adapter may project the typed reader, but it must remain a transport
projection rather than a second event bus. Before implementation it must define:

- authorization for database profile, generation, workspace scope, agent/run visibility,
  and operation identity;
- an opaque public subscription identifier rather than trusting client-composed
  partition keys;
- resume semantics based on `StreamSequence`;
- explicit wire representations for events, gaps, completion, eviction, and unknown
  streams;
- bounded connection/backpressure policy and cancellation on profile change;
- sanitized public messages and failure codes;
- reconnect behavior that directs clients to durable run APIs after ephemeral retention
  expires.

SSE must not imply that the in-memory activity history is durable, complete, or an audit
record.

## Source And Test Anchors

Primary implementation:

- `src/MAF/Common/CanDoItAll.AgentFramework.Models/Conversations/AgentExecutionActivityModels.cs`
- `src/Foundation/CanDoItAll.SharedKernel/Streaming/PartitionedSequencedStream.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentExecutionActivityCoordinator.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentExecutionActivityTransitionRules.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core/Preparation`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/ProviderRuntimeProfileSnapshotService.cs`
- `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureInvocationSnapshot.cs`
- `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureInvocationSnapshotReadDispatcher.cs`
- `src/Modules/CanDoItAll.Modules.Processes/AgentChat/ProcessInvocationSnapshot.cs`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentExecutionActivityStatus.razor`

Focused tests:

- `tests/Unit/CanDoItAll.Tests.Unit/AgentExecutionActivityCoordinatorTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/PartitionedSequencedStreamTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/AgentExecutionPreparationServiceTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/ProviderRuntimeProfileSnapshotServiceTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/ProjectStructureInvocationSnapshotTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/ProcessInvocationSnapshotTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/AgentExecutionActivityStatusTests.cs`
- `tests/Integration/CanDoItAll.Tests.Integration/FileSandboxWorkspaceChatRunCommitRecoveryIntegrationTests.cs`
