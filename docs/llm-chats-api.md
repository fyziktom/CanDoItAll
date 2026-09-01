# LLM Chats Product And API

LLM Chats is the ordinary multi-turn chat product boundary. It uses the provider-neutral invocation
runtime and canonical PostgreSQL transcript, but it does not create agents, agent runs, tools, skills,
memory, processes, workspaces, or provider-native conversation state. It exposes both typed Blazor
surfaces and an external HTTP API; hosting the UI inside the AgentFramework shell does not convert a
Simple Chat into agent execution.

## Product Model

An LLM Chat definition is reusable lifecycle-managed configuration. Every edit appends an immutable
definition revision. A conversation pins the current revision when it is created, so later definition
edits do not change existing conversations. Definitions can be activated, suspended, reactivated, and
archived; conversations can be renamed and archived.

The canonical transcript is PostgreSQL state. Turn admission atomically creates the durable operation,
active-turn state, user message, and first operation event. The HTTP request then returns `202 Accepted`;
it never owns the provider call. A hosted dispatcher claims pending work through a renewable database
lease, executes it outside the admission transaction, and persists the assistant result and terminal
operation state atomically. Expired leases are recoverable by another host. The runtime also uses the
application database-profile generation as a fence: a profile switch cancels in-flight work and requires
an explicit recovery decision when provider dispatch may have occurred.

## Product UI

The primary UI is the **Simple Chats** tab at `/agents?tab=simple-chats`. The former `/chats` route is a
compatibility redirect that preserves supported definition, conversation, and view query state.

- The definitions view lists, creates, edits, activates, suspends, and archives reusable definitions.
- The conversations view creates and resumes revision-pinned conversations, follows durable turn
  progress, and exposes explicit cancellation or recovery actions when required.
- The shared conversation shell can open a Simple Chat as a floating conversation without moving
  execution or persistence into the component layer.
- The composer can open Prompt Gallery through the AgentFramework-owned action contributor.
- AgentFramework overview analytics can report Agents, Simple Chats, or both, including known token and
  cost evidence and explicit unpriced observations.

The server-side UI orchestrates typed application services. Remote automation uses the HTTP routes
below; UI components do not duplicate provider execution, transaction, lease, or transcript rules.

When API authorization is enabled, an anonymous trusted-local browser receives only
the three Simple Chats scopes in its circuit-local access identity. OS headless mode
does not disable this browser capability. Existing authenticated users retain their
actual scopes; API requests and authorized-file HTTP routes still require authentication.
See [local container browser access](operations/containers.md#local-browser-access-with-api-authorization-enabled)
for the explicit, loopback-published Docker ingress configuration.

## Runtime Bounds And Configuration

LLM Chat execution remains a database-backed queue. `LlmChats:Dispatcher:WorkerCount` starts a fixed
number of independent claim workers (default `1`, allowed `1` through `32`); it does not create an
in-memory work queue. Each worker owns a distinct durable lease identity, the existing conversation
claim invariant prevents concurrent turns for one conversation, and host shutdown waits for every
started worker to drain. Registration means the host can accept work, while progress/saturation
telemetry reflects workers currently executing claimed operations.

The following option sections bind from configuration, retain the listed safe defaults when omitted,
and are validated during host startup:

| Section | Setting | Default | Validation summary |
|---|---|---:|---|
| `LlmChats:Dispatcher` | `PollInterval` | 1 second | 100 ms through 30 seconds |
| `LlmChats:Dispatcher` | `HeartbeatInterval` | 2 seconds | 100 ms through 1 minute |
| `LlmChats:Dispatcher` | `LeaseDuration` | 10 seconds | At least three heartbeats; at most 10 minutes |
| `LlmChats:Dispatcher` | `CandidateBatchSize` | 16 | 1 through 100 |
| `LlmChats:Dispatcher` | `WorkerCount` | 1 | 1 through 32 |
| `LlmChats:Dispatcher` | `MaximumQueuedAge` | 5 minutes | At least one poll interval; at most 24 hours |
| `LlmChats:Dispatcher` | `MaximumOperationDuration` | 30 minutes | At least queued age; at most 7 days |
| `LlmChats:Streaming` | `MinimumChunkBytes` | 256 | Positive and no greater than maximum chunk |
| `LlmChats:Streaming` | `MaximumChunkBytes` | 8 KiB | No greater than the persisted event-text bound |
| `LlmChats:Streaming` | `MaximumCoalescingDelay` | 150 ms | Positive |
| `LlmChats:Streaming` | `MaximumResponseCharacters` | 400,000 | No greater than the canonical message-text bound |
| `LlmChats:Streaming` | `MaximumResponseBytes` | 1,600,000 | At least one chunk; no more than four bytes per configured character |
| `LlmChats:Streaming` | `MaximumDeltaEvents` | 4,000 | Positive |
| `LlmChats:Streaming` | `EventRetention` | 7 days | Positive |
| `LlmChats:Streaming` | `CleanupInterval` | 1 hour | Positive and no longer than retention |
| `LlmChats:Streaming` | `CleanupBatchSize` | 500 | 1 through 10,000 event rows |
| `LlmChats:Streaming` | `MaximumReplayPageSize` | 500 | 1 through 5,000 events |
| `LlmChats:Transfer` | `MaximumRecordsPerCollection` | 100,000 | 1 through 1,000,000 records |
| `LlmChats:Transfer` | `MaximumTotalRecords` | 250,000 | At least the per-collection limit; at most 2,000,000 records |

A queued operation older than `MaximumQueuedAge` fails durably with
`llm-chat.queue-age-exceeded` before any provider dispatch. Once provider dispatch might have occurred,
crossing `MaximumOperationDuration` records `llm-chat.operation-duration-exceeded` and preserves
`RecoveryRequired`; it is never silently redispatched. Transfer counts all nine collections before
materializing the graph, then validates enum values, relationships, usage, terminal state, and dispatch
evidence before writing the target database.

## Provider And Thinking-Effort Options

`GET /api/llm-chats/provider-options` returns a credential-free view of available provider profiles and
their models. Each model reports its own allowed thinking-effort values and the provider default. The
response never contains an endpoint, credential name/value, local path, or full `ProviderProfile`.

Definition mutations use one nullable typed `thinkingEffort` field:

- omitted or `null` means use the selected provider/model default at dispatch time;
- `"none"` is an explicit request to disable reasoning and is valid only when that model advertises it;
- `"low"`, `"medium"`, `"high"`, and other supported typed levels are validated against the selected
  model's capability set;
- an unsupported level returns `thinking-effort-not-supported`; it is never silently downgraded;
- thinking effort inside the free-form model-parameter JSON is rejected because the typed field is
  authoritative.

The implementation reuses `AgentReasoningEffortLevel`, `ProviderModelThinkingEffortCapability`, and
`AgentThinkingEffortPolicy` as the repository's provider/model capability contracts. Their historical
names do not activate agent execution. LLM Chats must not introduce a parallel effort enum, duplicate
provider catalog, model-name inference, or string-based domain policy. Invocation audit stores both the
requested nullable effort and the effective effort selected for dispatch.

## Routes

All routes are under `/api`:

| Route family | Behavior |
|---|---|
| `GET /llm-chats/provider-options` | Safe provider/model/effort options |
| `GET/POST /llm-chats` | Bounded definition list and definition creation |
| `GET/PUT /llm-chats/{definitionId}` | Definition read and append-only revision update |
| `GET /llm-chats/{definitionId}/editor` | Manage-scoped authoritative editable definition revision |
| `POST /llm-chats/{definitionId}/{activate|suspend|archive}` | Definition lifecycle transition |
| `POST /llm-chats/{definitionId}/conversations` | Conversation creation pinned to the current revision |
| `GET /llm-conversations` | Bounded conversation list |
| `GET /llm-conversations/{conversationId}` | Conversation plus bounded transcript page |
| `PATCH /llm-conversations/{conversationId}/title` | Revision/concurrency-checked rename |
| `POST /llm-conversations/{conversationId}/archive` | Concurrency-checked archive |
| `POST /llm-conversations/{conversationId}/turns` | Retry-safe turn admission and execution |
| `GET /llm-chat-operations/{operationId}` | Durable operation status, result, and invocation evidence |
| `GET /llm-chat-operations/{operationId}/events` | Durable replay followed by live SSE until terminal state |
| `POST /llm-chat-operations/{operationId}/cancel` | Durable cancellation request plus local live-call signal |
| `POST /llm-chat-operations/{operationId}/reconcile` | Manage-scoped settlement from durable evidence after the live owner has drained |
| `POST /llm-conversations/{conversationId}/active-turns/{turnId}/abandon` | Exact recovery after the live owner has drained |

The running contract is authoritative at `/openapi/v1.json` and the byte-identical
`/swagger/v1/swagger.json`; interactive Swagger UI is served at `/swagger` when enabled. The maintained
cross-repository snapshot and the `candoitall-api-llm-chats` operator skill live in
`CanDoItAll.SharedInfo/codex/skills`.

Definition and conversation resources return strong numeric ETags. Mutation requests must supply the
matching expected token in the body or `If-Match` where supported. Lists and transcript messages use
bounded `take` values and opaque cursors. Read-scoped transcript pages exclude persisted `System`
messages before cursor paging. The manage-scoped editor resource is the only definition read contract
that returns the system prompt; its allowlisted response never includes provider credentials, endpoints,
or local paths.

## Turn Idempotency And Failures

Every turn requires a caller-supplied non-empty `operationId`, expected transcript revision, and bounded
message. The operation ID is the durable idempotency identity. Repeating the same request returns the
same committed operation/result without another provider dispatch, message, or audit record. Reusing
the ID with a different fingerprint returns `operation-id-conflict`. A stale transcript revision returns
`transcript-revision-conflict`.

Successful turn admission always returns `202` with `Location` pointing to the operation resource and
links for status and events. Admission fails with `503` when no dispatcher is registered, rather than
accepting work that cannot progress. Failures use stable Problem Details codes, the operation ID when
applicable, and a typed retryability flag. Public operation resources do not expose the internal request
fingerprint. Raw provider exceptions, credentials, endpoints, paths, system instructions, and prompts
are not returned.

Operation status includes at most 100 invocation attempts ordered by ordinal. Each attempt is an
allowlisted projection of provider kind, model, delivery mode, bounded finish reason, requested and
effective reasoning effort, outcome, usage, stable failure metadata, and timestamps. Provider profile
identity/name, correlation IDs, and raw failure details remain internal.

Reconciliation never calls a provider or redispatches ambiguous post-dispatch work. It rejects an
operation with a live execution lease, settles succeeded, failed, or cancelled state only when the
durable transcript/invocation evidence proves that outcome, and otherwise preserves
`recovery-required` for an explicit follow-up decision.

## Operation Event Stream

`GET /api/llm-chat-operations/{operationId}/events` uses the shared Web SSE writer. Event IDs are the
operation journal's monotonically increasing sequences. Reconnect with `Last-Event-ID` or the `after`
query parameter; when both are present they must contain the same non-negative value. The server first
replays retained committed events, emits `stream.gap` with the operation status URL when the cursor is
outside retained history, then follows new committed events. Heartbeat comments keep an idle stream
alive, buffering is disabled, and the connection closes after the terminal event is delivered.

The operation row owns the durable event high-water mark. Event append advances that mark in the same
database transaction as the journal row, so full event retention and process restart cannot reset the
next event ID or hide a replay gap.

Replay reads operation state, result metadata, retained events, and the durable high-water from one
short repeatable-read snapshot; the transaction is never held while waiting for a live notification.
Retention deletes only expired terminal operation-event rows, counts the configured batch in event
rows, and immediately drains another bounded batch when the previous batch was full. Canonical
transcript and invocation-audit rows are not retention targets. Process-local signal and
profile-generation schedules are bounded accelerators only; durable journal polling remains the
correctness path if an idle accelerator entry is evicted.

The `candoitall.llm-chat-operation-event.v1` envelope exposes accepted/claimed, provider-attempt,
response-delta/completed, cancellation, success, failure, and recovery-required events. Deltas become
canonical transcript content only when the success transaction commits. A client disconnect stops only
that response; it does not cancel or redispatch the durable operation. Cancellation requires the
operation cancel route. Bearer credentials are accepted through the normal authorization header or the
host's canonical cookie flow, never an SSE query parameter.

## Persistence, Transfer, And Operations

Nine `LlmChats_*` PostgreSQL tables own definitions, immutable revisions, tags, conversations,
transcripts, messages, operations, invocation audit, and the durable operation-event journal. The
schema, lease/cancellation metadata, retention behavior, event sequences, model snapshot, and canonical
database-transfer export/import graph move together through the normal PostgreSQL migration path. No
file-backed chat store is registered in production.

The API follows the Web host's canonical authorization policy. When bearer authorization is enabled,
routes require the exact `api.llm-chats.read`, `api.llm-chats.manage`, or `api.llm-chats.execute` scope;
the broad `api` scope is not an LLM Chat super-scope. HTTP conversation creation accepts no origin field
and always persists `Api` origin. The checked-in trusted-local profile may leave bearer authorization
disabled; any remotely reachable deployment must enable the canonical API authorization configuration
before exposing these routes.

Focused verification is documented in [Testing](testing.md). Project Structure context, public chatbot
deployment, retrieval/RAG, moderation, and external participants remain explicitly deferred. Current UI
ownership and the remaining handoffs are recorded in
[LLM Chats boundary and integration ownership](architecture/llm-chats-boundary-and-handoffs.md).
