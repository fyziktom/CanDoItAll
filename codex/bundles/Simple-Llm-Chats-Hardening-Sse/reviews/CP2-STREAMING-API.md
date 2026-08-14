# CP2 — Streaming API review

State: **Locked until SB11**

| Criterion | Result | Evidence |
|---|---|---|
| OpenAI incremental stream proven | Pending | |
| Azure OpenAI incremental stream proven | Pending | |
| Ollama incremental stream proven | Pending | |
| Completed fallback explicitly identified | Pending | |
| Retry stops after first accepted delta | Pending | |
| Attempt usage/outcome audit is exact | Pending | |
| Durable event sequence and coalescing proven | Pending | |
| 202 returns before provider completion | Pending | |
| Request/SSE disconnect does not cancel operation | Pending | |
| Last-Event-ID replay and gap proven | Pending | |
| Terminal event closes stream | Pending | |
| Profile switch closes old stream and blocks commit | Pending | |
| External scopes and origin proven | Pending | |
| Linux/UTF-8/chunk framing proven | Pending | |
| No canonical partial assistant message on failure | Pending | |

Decision:

- `Ready — unlock SB12`
- `Not Ready — keep closure locked`
