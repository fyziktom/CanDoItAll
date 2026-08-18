# Security And Profile Fence

- UI actions use an authorization facade aligned to `ReadLlmChats`, `ManageLlmChats`, and `ExecuteLlmChats` policy semantics.
- Read-only users never receive the system prompt or mutation controls.
- Provider options expose only allowlisted profile/model metadata, not credentials.
- Markdown raw HTML remains disabled and unsafe URI schemes are removed.
- Failure surfaces use typed sanitized errors; exception messages/provider bodies are not rendered directly.
- Active operation ids and event sessions are resolved inside the current database profile generation.
- Profile changes cancel the UI event-session lease and clear old-profile projections.
- No project context is inferred from the current route, tab, selected node, or Agent context registry.
