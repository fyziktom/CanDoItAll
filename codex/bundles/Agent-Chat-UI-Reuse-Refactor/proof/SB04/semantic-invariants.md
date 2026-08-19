# SB04 semantic invariants

- Thread ordering remains newest-first and search remains case-insensitive across title, last-message preview, and message-count metadata.
- The selected thread remains stable through an opaque key and maps back to the exact Agent session Guid before orchestration.
- Thread title, `dd.MM HH:mm` rail time, empty-thread/message-count copy, 88-character preview, full tooltip, approval badges, and auto-approve badge remain unchanged.
- History remains newest-first, capped at 25 rows, returns the clicked session Guid, and preserves title fallback, 170-character preview, run-evidence, approval, message-count, selected, and local-time presentation.
- Existing `agent-thread-*` CSS hooks and `data-testid` selectors remain in rendered neutral markup.
- Loading, error, empty, and no-match states are explicit and source-neutral; action slots do not own Agent effects.
- No AgentFramework, LlmChats, persistence, backend service, Guid, or service-locator type exists in neutral production source.
- Participant compatibility hardening preserves the original `Show agent details` accessible label and honors the public `IsFavorite` parameter exactly.
