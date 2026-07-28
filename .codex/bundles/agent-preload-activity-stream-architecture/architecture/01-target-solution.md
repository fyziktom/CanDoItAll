# Target Solution

## Architecture stance

Do not pool live agents. A MAF runtime build owns provider/session state, tools, MCP connections, disposables, current authorization, context contributors, and pending approvals. Reusing that object graph would trade startup latency for correctness and security defects.

The target has three separate concepts:

1. a bounded typed in-process event-stream primitive;
2. an agent-specific operational activity lifecycle projected through that primitive;
3. immutable revisioned snapshots used as read-only preparation inputs.

The existing `IActivityStream` persists user/business activity entries and is not reused as an operational bus. Only execution run/detail/log persistence is canonical run history. Existing `ExecutionEvent` and the new operational stream are projections. Operational activity is ephemeral feedback and measurement; durable persistence is not on its publication path and no asynchronous activity consumer writes canonical logs.

## Shared typed stream primitive

`CanDoItAll.SharedKernel` owns transport-neutral typed contracts for a partitioned sequenced stream. The primitive has:

- generic, strongly typed partition and event types;
- monotonic sequence per partition;
- bounded retention;
- ordered reads from an explicit sequence;
- an explicit typed gap when a reader falls behind retention;
- cancellation-aware async reading;
- deterministic subscription/disposal;
- a singleton bounded partition store with terminal eviction and never active-operation idle eviction;
- a global partition limit that returns typed capacity rejection when only active partitions remain;
- bounded terminal replay TTL/count, bounded tombstone TTL/count, and distinct `Gap`, `Evicted`, and `Unknown` read results;
- publisher work limited to append-and-signal, never awaiting UI callbacks.

The primitive alone assigns sequence under the partition lock and returns the sequenced envelope; domain payloads never carry a second sequence. It does not know agent phases, authorization, SSE, MQTT, UI strings, EF, or persistence. Modules expose domain-specific coordinator/reader interfaces rather than injecting a global service-locator-style bus.

## Agent operational lifecycle

Agent Framework Models/Core own:

- `AgentExecutionOperationId`;
- `AgentExecutionActivityStreamId`, containing only stable database-profile/workspace scope plus operation ID;
- `AgentExecutionActivityPhase` enum;
- `AgentExecutionActivityTerminalOutcome` with `Succeeded`, `Failed`, `Cancelled`, and `Suspended`;
- immutable `AgentExecutionActivity`;
- separate coordinator/reader interfaces.

The application orchestrator exposes synchronous `StartSendMessage` and `StartApprovalContinuation` entry points. Each validates input, resolves the stable execution profile/workspace scope, admits/allocates an operation, starts the asynchronous command, and immediately returns `AgentChatOperationHandle(StreamId, Completion)`. UI reads replay from sequence zero; cancelling/disposal of a reader never cancels the command. Typed admission rejection prevents execution when capacity is exhausted.

Agent, session, context source/version, and run identity are event metadata populated as they become known; they are not partition fields. A typed activity context is required through `AgentChatRunOptions` on every new production entry path. `ExecutionRunRequest` requires `InitialActivityOperationId`; only the persisted `ExecutionRunRecord.InitialActivityOperationId` is nullable for legacy deserialization. Core never silently generates a missing operation or substitutes a null coordinator.

One operation means one command invocation. An invocation that reaches approval ends with typed terminal outcome `Suspended`; approval/denial continuation allocates a new operation linked to the same execution run. Operations are never reopened across calls or process restarts. Disposing an unterminated operation lease terminalizes it as `Cancelled`; normal paths must explicitly complete/fail/suspend it.

Required phases are semantically stable enums, not localized strings:

- `Accepted`;
- `CapturingContext`;
- `ResolvingPreparation`;
- `ResolvingProvider`;
- `ResolvingSession`;
- `CreatingExecution`;
- `PreparingInput`;
- `PreparingCapabilities`;
- `PreparingRuntime`;
- `WaitingForProvider`;
- `Streaming`;
- `UsingTool`;
- `AwaitingApproval`;
- `PersistingResult`;
- `Completed`;
- `Failed`;
- `Cancelled`.

Human-readable `Message` is UI text and may evolve. State transitions are validated: one acceptance, optional run binding, monotonically sequenced progress, and exactly one terminal activity. Terminal failure/cancellation remains observable even when no run could be created.

`AgentExecutionActivityCoordinator` owns per-operation phase validation, run binding, and terminal compare-and-swap. Producers receive an operation-bound lease and report unsequenced facts; they cannot publish envelopes or terminal transitions directly. Activity publication failures and durable persistence failures are distinct. A faulty or slow UI reader cannot change the canonical execution outcome. Existing synchronous `ExecutionUpdated` relays are adapted through isolated notification and progressively reduced to compatibility projections.

## Revisioned preparation snapshots

Preparation stores immutable blueprints, not runtime objects. A blueprint may contain:

- defensively copied agent definition;
- provider descriptor identity and non-secret fingerprint;
- capability/memory catalog identities from one canonical catalog data revision;
- workspace/profile scope;
- preparation revision, creation time, expiry/freshness policy, and dependency fingerprint.

It must not contain:

- credentials or secret values;
- provider clients/handles;
- `DbContext` or store transaction state;
- live agents, MCP clients, tools, disposables, runtime sessions, pending approvals;
- transient authorization decisions;
- output of request-specific context contributors;
- mutable collection instances.

The preparation service is a scoped, bounded immutable map keyed by typed `(profile/workspace, agent)` blueprint key with per-key single-flight and atomic per-key commit. The file catalog owns one monotonically increasing data revision advanced under the store lock after every successful relevant canonical catalog write; database-profile generation and provider fingerprint complete the dependency identity. Context revision is excluded because context is captured per invocation. In-flight results commit only when their captured invalidation generation is current. A service-owned lifetime CTS owns shared factory work; each waiter cancels only its own `WaitAsync`. Scope disposal cancels outstanding shared work. Expired or stale snapshots fail or refresh explicitly; there is no silent stale fallback.

Each send builds a transient per-invocation startup aggregate from the validated blueprint plus current session and captured context; session/context never enter the bounded preparation map. Catalog revision, profile generation, and provider fingerprint are validated again immediately before capability/policy materialization. Non-security configuration is snapshot-isolated for the started operation. Authorization, secrets, and tool access remain per-run/current at their existing enforcement boundaries and are never authorized from the blueprint.

## Module runtime snapshots

`AgentChatContextRegistry` remains the only scoped aggregation and publication owner. Models define an empty marker `IAgentChatContextAttachment` and typed `AgentChatContextAttachmentKind`, `ModulePublicationRevision`, `SnapshotContentFingerprint`, `SnapshotCoverageFingerprint`, `DatabaseProfileGeneration`, and `AgentChatContextAttachmentEnvelope` contracts. Concrete project/process immutable attachment records remain in their owning modules. The registry stamps an envelope with the captured scope ID, source identity, workspace scope, typed contributor ID and attachment kind, publication revision, content fingerprint, coverage fingerprint, database-profile generation, capture time, freshness deadline, and opaque attachment.

Three values have deliberately different semantics:

- `ModulePublicationRevision` is monotonically increasing per contributor registration and orders atomic publications. Publishing identical content still advances it.
- `SnapshotContentFingerprint` is stable for identical captured authorized data and selection only. It changes when a prompt/tool-visible value or selection changes, but not merely because profile generation, coverage, or publication time changes.
- `SnapshotCoverageFingerprint` is stable for an identical normalized coverage descriptor only. It changes when covered identities, fields, detail classes, or time windows change, even when the overlapping content is unchanged.
- `DatabaseProfileGeneration` identifies the canonical database-profile generation independently of content and coverage.
- No snapshot stamp is a canonical mutation token. Publication revision, content fingerprint,
  coverage fingerprint, freshness, and database-profile generation are projection facts only.
  They must never be passed to a canonical write service as optimistic-concurrency evidence.

`AgentChatContextAttachmentFreshness` classifies an envelope as `Current`, `Expired`, or `ProfileMismatch` against the current clock/profile generation. Only `Current` attachments are eligible for snapshot-backed tool dispatch. Expiry or profile mismatch is an explicit result/activity; it never silently recaptures or falls back to storage.

The existing registry lock replaces a contributor's fragment and immutable attachment envelopes together and captures immutable arrays. Multiple contributors/types are supported, with at most one concrete attachment type per contributor registration. Core transports attachments opaquely and provides generic type-safe enumeration; it never uses string keys, `Dictionary<Type, object>`, or module references. A second module snapshot store is not introduced and `Interlocked.Exchange` is not required where the registry lock owns publication. Builders fully construct immutable collections before registration and never mutate a captured object. An invocation captures one publication; a later UI edit cannot mutate that request.

The existing propagation path is extended rather than replaced:

`AgentChatContextSnapshot` → `AgentChatContextInvocationFactory` → `AgentChatRunOptions.TransientContext` → `ExecutionRunRequest.TransientContext` → `AgentRunTransientContextRegistry` → `AgentRuntimeExecutionOptions.TransientContext` → `AgentRuntimeToolProviderContext`.

`AgentRuntimeTransientContext` owns the immutable envelopes as non-serialized invocation state, and `AgentRuntimeToolProviderContext` exposes generic typed lookup. The transient-context SHA-256 digest covers normalized prompt-context content plus ordered envelope identity and integrity fields: `(ScopeId, Source, WorkspaceScope, ContributorId, AttachmentKind, ModulePublicationRevision, SnapshotContentFingerprint, SnapshotCoverageFingerprint, DatabaseProfileGeneration, CapturedAtUtc, FreshUntilUtc)`. The persisted invocation manifest stores only that combined digest, never concrete attachment payloads. Approval continuation resolves the original in-memory lease and verifies the same combined digest; if the lease is gone or any attachment differs, continuation fails explicitly as it does for missing transient context today.

Each module derives two representations from the same immutable publication:

- a bounded/redacted prompt fragment;
- a fuller authorized tool snapshot whose explicit coverage descriptor states which identities, fields, selections, and time window it can answer without storage.

Tool dispatch is explicit and never chooses an attachment by type alone. Eligibility requires all of:

- the captured invocation scope ID, source kind/id, and workspace scope match the envelope;
- the exact expected typed contributor ID and attachment kind match;
- generic lookup resolves the exact expected concrete attachment type;
- envelope profile generation equals the current runtime profile generation;
- freshness is `Current`;
- the typed coverage descriptor covers the requested identities, fields/detail class, and time window.

Lookup returns a typed reason for `NotFound`, `SourceMismatch`, `ScopeMismatch`, `ContributorMismatch`, `KindMismatch`, `TypeMismatch`, `ProfileMismatch`, `Expired`, or `CoverageMiss`. An eligible request must use the snapshot and make zero persistence calls. An ineligible request never queries storage. Project Structure exposes a typed `ProjectStructureReadSource` choice: `InvocationSnapshot` is the default and `CanonicalCurrent` is an explicit deeper read. This lets the model deliberately request deeper canonical data during work without a hidden fallback.

Project Structure contributes `ProjectStructureInvocationSnapshot` through a pure adapter over
the already-held `ProjectStructureSurface` and selected-node state. That surface does not expose
a trustworthy canonical concurrency token: `UpdatedAtUtc` is currently a descriptive timestamp,
not an EF concurrency token. The adapter therefore publishes no mutation token and the runtime
snapshot is ineligible for every write path. Project mutation tools must cross the existing
canonical application-service boundary, load/authorize the affected current canonical entities
inside that service's transaction, and publish a fresh UI projection after the commit. Adding
true row-version concurrency for Project Structure is a separate canonical-domain change that
must cover UI and agent writers together; fabricating it only for this preload projection would
create asymmetric write semantics and a false safety claim.

Process Workspace and Live Processes contribute `ProcessWorkspaceInvocationSnapshot` with a typed present/absent revision vector. It covers every prompt/tool-visible source, not only selected-run detail:

| Vector component | Sole ownership |
| --- | --- |
| `SurfaceSelection` | source/surface, project/global scope, access state, current workspace/live view, run subview, route/effective selection identity, status filter, and files selection |
| `ShellRefresh` | projection refresh status/generation |
| `DefinitionCatalog` | published/draft counts and selected-definition identity/name, status, scope, criticality, operating mode, and compatibility count |
| `LiveRunSummary` | active, attention, and failed run counts |
| `EffectiveLiveRun` | loaded-run count plus the exact effective/focused live-run identity, status, project/name, subprocess, progress, and current-step facts |
| `SelectedRunDetail` | selected/focused runtime detail projection |
| `SelectedRunRecord` | record identity, `SourceGlobalSequence`, `SourceRootSequence`, and `UpdatedAtUtc` |
| `RuntimeHistory` | workspace/live history window, page/filter state, events, metric events, and has-more state |
| `FocusedEvent` | focused event identity, type, sensitivity, and detail |
| `FocusedAgent` | focused active-agent identity/display name/executor, status, step, role, and detail |
| `TelemetryObservation` | telemetry observation identity/window/source sequence |
| `DerivedFacts` | fingerprints of `Stats`, `MetricPoints`, `ToolUsage`, `Summary`, and `AttentionSummary` |

Each component is explicitly `Present(value)` or `Absent(reason)`; nullable/default maxima are not revisions. Surface construction and the snapshot mapper share a field-to-component coverage table. The completeness test enumerates every emitted position fact, entity reference, prompt fragment field, and tool snapshot field, then proves that changing each source changes exactly its declared component or the declared derived fingerprint. A maximum timestamp/sequence is never treated as a coherent shell revision.

Snapshot content is a read-only prompt/tool input and never participates in write-back. Completion only invalidates/reloads canonical module state through a current-generation fence.

## Safe startup concurrency

Prefer removal/coalescing of duplicate catalog/session reads before adding tasks.

Permitted overlap:

- provider lookup and selected-session read only after provider resolution consumes the already-loaded catalog and proves it will not fall back to the same file store;
- independent immutable preparation acquisition and current context capture;
- independent initialized split-store projections when revision consistency is not required and the result records its revisions.

Forbidden overlap:

- operations on one `DbContext`;
- writes to one workspace;
- multi-file reads that promise one coherent revision;
- runtime capability stages or tool providers that mutate ordered composition state;
- progress callbacks that update the same run detail.

Failure precedence, cancellation ownership, and partial-result disposal are explicit. Shared single-flight work uses a service-owned lifetime; each waiter may cancel its own wait without cancelling other callers.

## Persistence and feedback

The run store remains canonical. The operational stream publishes immediately and records measurements independently. Existing durable execution-log semantics are preserved in this bundle. Coalescing is a separate future decision only if measurements prove a material bottleneck and an explicit audit-equivalence policy exists.

The existing throwing-multicast hazard is removed: subscriber exceptions are isolated and logged with event/operation identity, cannot reverse a successful store commit, and cannot prevent other projections. Profile switches unsubscribe old workspaces.

## Blazor projection

Only after the backend gate passes:

- `AgentChatPanel` subscribes through a scoped authorized reader by stable partition/operation, not by currently selected run;
- Process Manager chat uses the same orchestrator/activity lifecycle instead of a direct special path;
- existing Radzen/BaseLib wrappers render current phase, message, elapsed time, and error/approval state;
- activity remains compact in the current run-state region and does not introduce another scroll owner.

The UI never parses phase strings or infers state from spinner visibility.

## Future SSE boundary

SSE remains out of scope. The later API adapter will:

- authorize the requested workspace/profile/agent/operation stream before subscribing;
- project typed envelopes to versioned API DTOs;
- resume from sequence/`Last-Event-ID`;
- surface a typed retention gap that requires resynchronization;
- never expose an unrestricted all-agent stream;
- remain a projection, not a canonical event store.

After process restart or terminal eviction, SSE resynchronizes from canonical run persistence and cannot replay lost ephemeral preparation detail. No SSE transport concern is added to domain events or the shared stream implementation in this bundle.
