# LLM Chats Backend API

LLM Chats is the ordinary multi-turn chat product boundary. It uses the provider-neutral invocation
runtime and canonical PostgreSQL transcript, but it does not create agents, agent runs, tools, skills,
memory, processes, workspaces, or provider-native conversation state. This release is backend/API only;
it adds no Razor or floating-agent-chat integration.

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
| `POST /llm-conversations/{conversationId}/active-turns/{turnId}/abandon` | Exact recovery after the live owner has drained |

Definition and conversation resources return strong numeric ETags. Mutation requests must supply the
matching expected token in the body or `If-Match` where supported. Lists and transcript messages use
bounded `take` values and opaque cursors.

## Turn Idempotency And Failures

Every turn requires a caller-supplied non-empty `operationId`, expected transcript revision, and bounded
message. The operation ID is the durable idempotency identity. Repeating the same request returns the
same committed operation/result without another provider dispatch, message, or audit record. Reusing
the ID with a different fingerprint returns `operation-id-conflict`. A stale transcript revision returns
`transcript-revision-conflict`.

Successful turn admission always returns `202` with `Location` pointing to the operation resource and
links for status and events. Admission fails with `503` when no dispatcher is registered, rather than
accepting work that cannot progress. Failures use stable Problem Details codes, the operation ID when
applicable, and a typed retryability flag. Raw provider exceptions, credentials, endpoints, paths,
system instructions, and prompts are not returned.

## Operation Event Stream

`GET /api/llm-chat-operations/{operationId}/events` uses the shared Web SSE writer. Event IDs are the
operation journal's monotonically increasing sequences. Reconnect with `Last-Event-ID` or the `after`
query parameter; when both are present they must contain the same non-negative value. The server first
replays retained committed events, emits `stream.gap` with the operation status URL when the cursor is
outside retained history, then follows new committed events. Heartbeat comments keep an idle stream
alive, buffering is disabled, and the connection closes after the terminal event is delivered.

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

Focused verification is documented in [Testing](testing.md). UI, shared-component isolation, Project
Structure context, public chatbot deployment, retrieval/RAG, moderation, and external participants are
explicitly deferred. Their ownership boundaries are recorded in
[LLM Chats architecture and future handoffs](architecture/llm-chats-boundary-and-handoffs.md).
