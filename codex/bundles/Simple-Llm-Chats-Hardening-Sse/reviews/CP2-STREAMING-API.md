# CP2 — Streaming API review

State: **Ready — unlock SB12**

Implementation commit: `4ec4d2694d980d52936b4679ae676a0624d5c6fb`
Linux architecture snapshot: `snap-20260815080824-3b5bd776`

| Criterion | Result | Evidence |
|---|---|---|
| OpenAI incremental stream proven | Ready | Fragmented Chat Completions and Responses SSE parser cases pass on Linux. |
| Azure OpenAI incremental stream proven | Ready | Azure fragmented SSE uses the same neutral incremental contract and passes. |
| Ollama incremental stream proven | Ready | Fragmented LF/CRLF NDJSON, thinking suppression, usage, and done-frame cases pass. |
| Completed fallback explicitly identified | Ready | Adapter emits one delta/completion labelled `CompletedFallback`. |
| Retry stops after first accepted delta | Ready | Retry-before-delta and no-retry-after-visible-delta cases pass. |
| Attempt usage/outcome audit is exact | Ready | Per-attempt ordinal/outcome/usage and direct/recovery reduction cases pass. |
| Durable event sequence and coalescing proven | Ready | PostgreSQL concurrent sequence, rollback/no-signal, UTF-8 coalescing, retention, and transfer pass. |
| 202 returns before provider completion | Ready | Real host admits while controllable provider is blocked and exposes canonical links. |
| Request/SSE disconnect does not cancel operation | Ready | Request disconnect and stream reconnect complete with one provider dispatch. |
| Last-Event-ID replay and gap proven | Ready | Reconnect excludes the consumed delta; retained-history deletion emits `stream.gap` with status URL. |
| Terminal event closes stream | Ready | Success, cancel, and failure streams each close after one terminal event. |
| Profile switch closes old stream and blocks commit | Ready | PostgreSQL profile fence retains usage, rejects finalization, and closes the old stream. |
| External scopes and origin proven | Ready | Exact read/manage/execute and server-owned Api origin cases pass in the Linux Integration union. |
| Linux/UTF-8/chunk framing proven | Ready | Ubuntu package-mode build plus fragmented UTF-8 parser and real SSE framing/heartbeat cases pass. |
| No canonical partial assistant message on failure | Ready | Partial-provider failure retains incomplete event evidence and commits no assistant message. |

## Semantic conclusion

Provider drivers own wire parsing, ProviderRuntime owns retry/fallback, the LLM Chats product owns the
durable operation/event lifecycle, Persistence owns PostgreSQL authority, and Web owns only typed
HTTP/SSE transport. The real-host test now observes an endpoint heartbeat while the provider remains
blocked after its first delta. No production source, project reference, persistence model, or partial
class changed in SB11.

## Package-feed prerequisite

`UseLocalCanDoItAllLibraries=false` compiled the complete Web graph on Ubuntu with 0 warnings/errors
after the exact clean sibling Spreadsheet source was packed as 0.1.18 into a container-only feed. The
initial nuget.org-only restore failed because that package is not published. This does not invalidate
the streaming/API architecture decision, but SB13 must treat publication or an equivalent reviewed
dependency-source correction as a hard final-release prerequisite and must not claim the external CI
matrix green without it.

Decision:

- [x] `Ready — unlock SB12`
- [ ] `Not Ready — keep closure locked`
