# HTTP API delta

The exact route prefix follows the first implementation. Required semantics:

## Admit turn

```http
POST /api/llm-chat-conversations/{conversationId}/operations
```

Request includes:

- operation ID/idempotency key;
- expected conversation revision;
- user text;
- allowed model/settings overrides.

Response:

```http
202 Accepted
Location: /api/llm-chat-operations/{operationId}
```

Body includes:

- operation ID;
- conversation ID;
- current state;
- replayed flag;
- request fingerprint/idempotency representation;
- status URL;
- events URL;
- cancel URL.

Same ID/fingerprint returns the same operation representation without dispatching again.

## Operation status

```http
GET /api/llm-chat-operations/{operationId}
```

Returns a stable snapshot including terminal result/error and last event sequence.

## Events

```http
GET /api/llm-chat-operations/{operationId}/events
Last-Event-ID: 41
```

Supports documented `after` query alternative. Authorization and profile ownership are checked before
the response starts.

## Cancel

```http
POST /api/llm-chat-operations/{operationId}/cancel
```

Commits cancellation request and returns current snapshot. Repeated cancellation is idempotent. Terminal
success is not rewritten.

## Recovery

Keep an explicit recovery endpoint only for exact RecoveryRequired cases. It must not become a generic
“retry” that can double-dispatch.

## Compatibility

If the original synchronous endpoint remains temporarily, it must be marked compatibility-only,
implemented through admission plus wait/status, bounded by a documented timeout, and scheduled for
removal. It must not own provider execution.
