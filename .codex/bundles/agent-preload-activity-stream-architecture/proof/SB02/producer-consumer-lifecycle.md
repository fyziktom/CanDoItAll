# SB02 Producer, Consumer, and Lifecycle Matrix

## Scope rule

SB02 establishes the backend contract and authorized reader seam. The floating and
Process Manager UI do **not** consume the typed stream yet; that switch is owned by SB06.
Existing UI surfaces still consume the compatibility `ExecutionUpdated` event. No SSE
endpoint or external transport is claimed.

| Production artifact | Producer | Consumer | Automatic lifecycle | Adversarial/negative proof | Current boundary |
| --- | --- | --- | --- | --- | --- |
| `SequencedStreamEnvelope<AgentExecutionActivity>` | `AgentExecutionActivityOperation` appends through `PartitionedSequencedStream<AgentExecutionActivityStreamId, AgentExecutionActivity>` | `IAgentExecutionActivityReader`; module composition exposes `CurrentProfileAgentExecutionActivityReader` | Admission creates one partition; operation completion appends one terminal; terminal retention, terminal-capacity eviction, bounded tombstones, and expiry run during stream operations | `PartitionedSequencedStreamTests` cover order, fan-out, isolation, gap, all-active rejection, eviction, expiry, cancellation, and disposal | Production read API exists; UI/SSE subscription is deferred to SB06/SB07 |
| `AgentChatOperationHandle` | `AgentChatExecutionOrchestrator.StartSendMessage` and `StartApprovalContinuation` | Immediate-handle API intended for SB06; legacy async methods unwrap `Completion` | Handle is returned before context capture; operation owns completion and disposal | Blocked context capture proves handle/replay are available while completion is pending | No claim that current UI renders this handle yet |
| `IAgentExecutionActivityOperationLease` | Singleton `AgentExecutionActivityCoordinator.AdmitOperation` | Raw Core workspace facade, current-profile facade, orchestrator, and execution service | The admitting facade owns pre-dispatch resolution/dispatch failure terminalization and final disposal; the execution service owns normal dispatched success/failure/cancel/suspend outcomes; terminal CAS rejects a second terminal | Coordinator duplicate-terminal test; pre-I/O failure; raw Core/current-profile admission; operation-bound mismatch tests | No nullable production lease/coordinator path in static scan |
| `AgentExecutionOperationId` | Orchestrator allocates for handle paths; typed workspace execution-run callers allocate before direct workspace execution | Coordinator partition id, workspace request/options validation, persisted `ExecutionRunRecord.InitialActivityOperationId`, runtime `ActivityOperationId` | Initial id remains on the created workspace run; approval continuation allocates a distinct id bound to the same run | Default-id pre-I/O rejection, JSON legacy-null/round-trip, continuation identity, and static bypass scan | Legacy persisted workspace-run field alone may be null; direct runtime adapters that create no workspace run are not synthetic chat-activity producers |
| `AgentExecutionActivityContextIdentity` | Orchestrator binds captured `AgentChatContextSource` and snapshot version after `CaptureAsync` | All later envelopes from that operation; future module/UI/SSE readers | Single assignment; context is absent only before capture or when the registry returns no context | Coordinator binding test and blocked-capture orchestrator test | No string topic or object-bag context identity |
| Authorized activity reader | Module DI registers a scoped `CurrentProfileAgentExecutionActivityReader` over singleton canonical coordinator/stream | Future in-process module consumers and later SSE projection | Subscribe to database-profile changes, re-authorize after subscription, cancel pending read on profile switch, detach on reader disposal | Wrong profile/scope typed rejections, profile-switch cancellation, disposed-reader detach | Production facade exists; no external endpoint |
| Raw Core direct execution | `AgentFrameworkWorkspaceService` admits using required `AgentExecutionActivityWorkspaceIdentity` and coordinator | `AgentFrameworkWorkspaceExecutionService` operation-bound methods | Facade owns terminalization/disposal around the full task; Core validates workspace/agent/session/id before I/O | `AgentFrameworkWorkspaceActivityAdmissionTests` | Hosting registration supplies real singleton stream/coordinator/reader and explicit identity |
| Current-profile direct execution | `CurrentProfileAgentFrameworkWorkspaceService` confirms profile/scope identity and admits under `executionSubscriptionGate` before cold workspace-service construction | Pinned `IAgentFrameworkWorkspaceActivityExecutionService` | Facade owns terminalization/disposal; identity is rechecked after construction; profile change detaches the compatibility relay, advances its generation, and discards queued old-profile notifications; factory owns cached workspace-service disposal | Five blocked-cold-resolution entry cases, controlled profile-switch dispatch mutant, queued old-profile notification, factory disposal, and stale-resolution relay test | Cross-profile dispatch is rejected; no silent fallback to another service |
| Persisted initial operation correlation | `CreatePreparingRun`/chat-run creation set `InitialActivityOperationId` | Run storage, runtime option builder, continuation correlation and diagnostics | Initial id survives JSON round-trip; legacy missing property remains null | Identity tests plus continuation/targeted integration 3/3 | Durable history is correlation only; ephemeral stream events are not persisted as history |
| Compatibility `ExecutionUpdated` | Execution helper saves canonical detail, then calls `NotifyExecutionUpdated`; Core/workspace/current-profile relays publish through `IsolatedCompatibilityEventDispatcher` | `FloatingAgentChatCoordinator`, `AgentChatPanel`, `ContextualAgentWorkspaceWindows`, and the current-profile relay | Each subscriber has a bounded, generation-fenced mailbox; workers are queued independently; profile switch invalidates queued older-generation events; handler failures and overflow are logged with non-sensitive event identity; disposal stops subscriptions | SB01 failing-first, dispatcher slow/throw/overflow tests, queued old-profile generation test, current-profile race/slow tests, integration 5/5 | Compatibility only; not the canonical typed activity stream |
| Run event sink/runtime milestone | Execution helper publishes `IAgentExecutionEventSink` after persistence/compatibility enqueue; runtime begins later | Buffered/null sinks and actual runtime | Canonical work is awaited independently of compatibility callbacks | Integration test proves persisted Planning, later subscriber, event sink, runtime entry, and completed run despite a throwing subscriber | Integration baseline no longer claims callback order relative to asynchronous compatibility workers |
| Cached manually constructed workspace services | `CanDoItAllAgentWorkspaceFactory` creates services keyed by profile plus workspace scope and supplies coordinator/identity | Current-profile facade and module callers | Factory snapshots/clears under lock, disposes outside lock, aggregates failures, is idempotent, and rejects reuse | `AgentFrameworkWorkspaceFactoryDisposalTests` | Prevents event-dispatcher/subscription lifetime leaks |

## Production caller coverage

Within the workspace execution-run boundary, typed ids now enter the admitted facade
from:

- Web Agents API and the Web startup path;
- contextual agent workspace windows;
- floating-agent orchestration;
- HR process review and scenario harnesses;
- Cognitive Memory curator conversation;
- Process Manager chat/approval, process step execution, and run narrative generation.

`MafWorkflowLlmComponentInvoker` directly invokes `IAgentRuntime` and creates no Agent
Framework workspace run/session record. SB02 therefore does not claim it as a chat
activity producer. If that adapter later exposes user-facing agent activity, it must
create and surface an owned typed lifecycle rather than infer one from nullable runtime
options.

The exact caller paths are inventoried in `bundle://proof/SB02/source-assertions.md`.
The static transcript found five direct `AgentFrameworkWorkspaceService` constructions:
one production factory and four tests. Every construction supplies a coordinator and
workspace identity.
