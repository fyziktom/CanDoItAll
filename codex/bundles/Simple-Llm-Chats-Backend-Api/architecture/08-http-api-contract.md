# HTTP API contract

## Route family

Use a separate route family. Do not place ordinary chats under `/api/agents`.

### Definitions

```text
GET    /api/llm-chats
POST   /api/llm-chats
GET    /api/llm-chats/{definitionId}
PUT    /api/llm-chats/{definitionId}
POST   /api/llm-chats/{definitionId}/activate
POST   /api/llm-chats/{definitionId}/suspend
POST   /api/llm-chats/{definitionId}/archive
GET    /api/llm-chats/provider-options
```

### Conversations

```text
GET    /api/llm-conversations
POST   /api/llm-chats/{definitionId}/conversations
GET    /api/llm-conversations/{conversationId}
PATCH  /api/llm-conversations/{conversationId}/title
POST   /api/llm-conversations/{conversationId}/archive
```

### Turns and operations

```text
POST   /api/llm-conversations/{conversationId}/turns
GET    /api/llm-chat-operations/{operationId}
POST   /api/llm-chat-operations/{operationId}/cancel
POST   /api/llm-conversations/{conversationId}/active-turns/{turnId}/abandon
```

Active-turn abandonment is accepted only when the owning operation is durably `RecoveryRequired` (or
a strictly equivalent CP0-selected recovery state) and no current execution lease owns it. It is not a
shortcut for cancelling a live provider call.

The exact route names may follow a current repository convention discovered in SB00, but resource
separation and semantics are locked.

## Concurrency

- definition update requires a concurrency token or `If-Match`;
- conversation rename requires expected transcript/product revision;
- send requires expected transcript revision;
- responses expose the resulting revision and ETag where the Web conventions support it.

## Turn response

The first implementation may execute inline. Return a stable operation resource in all cases.

- completed inline: `200 OK` with operation and assistant result;
- admitted but not complete: `202 Accepted` with operation location;
- stale revision/idempotency conflict: `409 Conflict`;
- active turn already present: `409 Conflict`;
- invalid provider/settings: `422 Unprocessable Entity`;
- suspended/archived definition: `409` or repository-standard typed conflict;
- provider unavailable: `503 Service Unavailable`;
- deadline: repository-standard timeout mapping.

## Pagination

Definition and conversation list endpoints use bounded cursor or repository-standard page contracts.
Never return all transcripts by default. Conversation detail supports bounded message paging.

## API DTO boundary

Transport DTOs live in Web and map to module commands/results. They contain stable IDs, strings,
bounded settings, and expected revision. They never expose EF entities or generic internal transcript
documents directly. LLM Chat mutation DTOs must reject unmapped JSON members through a DTO-local
strict-deserialization mechanism (for example `JsonExtensionData` validation or an equivalent current
Web convention); do not change global JSON behavior without an explicit repository-wide decision. This
prevents a client from sending `context`, `attachments`, or model overrides that appear accepted but
are silently ignored.

Definition mutation DTOs expose a nullable typed `thinkingEffort` field. Missing/null selects provider
default; the explicit `none` value disables thinking only when the selected model advertises it.
Arbitrary model-parameter JSON cannot also set reasoning/thinking effort. The provider-options route
returns safe enabled-chat-provider and model projections with capability status, control mode, allowed
efforts, and configured provider default. It never returns credentials, endpoints, raw provider
configuration JSON, health diagnostics, or SDK objects.

## OpenAPI

Every route has:

- stable operation name;
- request/response schema;
- documented status codes;
- auth behavior;
- idempotency/concurrency headers;
- examples safe of secrets and real transcript data.
