# Operation State And Streaming Flow

```mermaid
sequenceDiagram
    participant UI as LlmChats.Ui
    participant App as LLM Chat application services
    participant DB as PostgreSQL / journal
    participant Worker as Dispatcher/provider

    UI->>App: Send(operationId, expectedTranscriptRevision, text)
    App->>DB: atomic admission + pending operation
    App-->>UI: accepted operation details
    UI->>App: open durable event session(operationId)
    Worker->>DB: accepted/claimed/attempt/delta events
    App-->>UI: replay/follow event pages
    UI->>UI: reduce deltas into transient Assistant projection
    Worker->>DB: terminal operation + canonical Assistant transcript
    App-->>UI: terminal event
    UI->>App: refresh operation + transcript
    UI->>UI: remove transient projections
```

## Invariants

- The operation id is generated once per logical send and retained through retry/reconnect.
- Event cursor advances monotonically; duplicate events are idempotent.
- Partial output is display-only.
- A gap resets partial output and reloads canonical state.
- UI follower disposal never cancels the operation.
- Only explicit cancel invokes `ILlmChatOperationApplicationService.CancelAsync`.
- A profile change terminates the follower fail-closed and reloads from the new profile; it never mixes generations.
