# SB01 semantic invariants

- Agent catalog and chat routes, selection, favorites, thread history, run selection, send/approval behavior, attachments, voice controls, runtime details, and settings remain Agent-owned behavior.
- The neutral project may render typed presentation state and raise typed callbacks; it may not locate services, execute agents, load persistence, call APIs, or branch on Agent/Simple Chat source kind.
- Existing `data-testid`, accessible names, focus order, scroll ownership, overlay controls, and action visibility are parity contracts.
- Floating lifecycle initialization stays non-blocking and cancellable; close/keep/stop decisions stay explicit.
- No production UI consumes `CanDoItAll.Modules.LlmChats`; no Simple Chat route/catalog/filter/API/SSE feature is activated.
- Extraction is complete only when the old Agent owner no longer renders the extracted neutral structure itself.

