# SSE event schema

Base envelope fields:

```json
{
  "schema": "candoitall.llm-chat-operation-event.v1",
  "operationId": "guid",
  "conversationId": "guid",
  "sequence": 42,
  "occurredAtUtc": "2026-08-14T00:00:00Z",
  "eventKind": "llm.response.delta",
  "operationState": "streaming",
  "payload": {}
}
```

## Event names

| Event | Purpose | Payload essentials |
|---|---|---|
| `llm.operation.accepted` | Durable admission | status/events URLs optional in HTTP response, not event |
| `llm.operation.queued` | Ready for dispatcher | queue timestamp |
| `llm.operation.claimed` | Worker ownership | sanitized owner/epoch, lease deadline |
| `llm.turn.admitted` | Pending user turn committed | turn ID, transcript revision |
| `llm.provider.attempt-started` | Dispatch attempt evidence | ordinal, provider kind, model |
| `llm.response.delta` | Assistant incremental output | text delta, aggregate character count |
| `llm.response.completed` | Provider completed | model, finish reason, usage |
| `llm.operation.cancellation-requested` | Durable cancel | cancellation generation/time |
| `llm.operation.succeeded` | Canonical answer committed | assistant message ID, transcript revision, usage |
| `llm.operation.failed` | Terminal failure | stable error code/category, retryability |
| `llm.operation.cancelled` | Terminal cancellation | stable reason |
| `llm.operation.recovery-required` | Operator decision needed | stable recovery category |
| `stream.gap` | Cursor fell outside retained range | earliest/current/resume cursor and snapshot hint |

## Wire requirements

- `id:` equals the stable resumable sequence.
- `event:` equals the event name.
- `data:` is JSON in Web defaults with string enums.
- heartbeat remains an SSE comment.
- terminal event is flushed before stream close.
- all responses use no-cache/no-store and disable proxy buffering through the existing writer.
