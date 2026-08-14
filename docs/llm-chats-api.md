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

The canonical transcript is PostgreSQL state. A turn appends one user entry and one assistant entry,
with an explicit active-turn marker between admission and completion. The runtime uses the application
database-profile generation as a lease fence: a profile switch cancels in-flight work and requires an
explicit recovery decision when provider dispatch may have occurred.

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

Inline completion returns `200`; admitted or running work returns `202` with `Location` pointing to the
operation resource. Failures use stable Problem Details codes, the operation ID when applicable, and a
typed retryability flag. Raw provider exceptions, credentials, endpoints, and paths are not returned.

## Persistence, Transfer, And Operations

Eight `LlmChats_*` PostgreSQL tables own definitions, immutable revisions, tags, conversations,
transcripts, messages, operations, and invocation audit. The schema is part of the normal PostgreSQL
migration path and the canonical database-transfer export/import graph. No file-backed chat store is
registered in production.

The API follows the Web host's canonical authorization policy. The checked-in trusted-local profile may
leave bearer authorization disabled; any remotely reachable deployment must enable the canonical API
authorization configuration before exposing these routes.

Focused verification is documented in [Testing](testing.md). UI, streaming, public chatbot deployment,
retrieval/RAG, moderation, external participants, and multi-instance background dispatch are explicitly
deferred to later bundles.
