# Semantic invariants — SB05

- Raw HTML stays disabled through one static Markdig pipeline; parameter changes rerender current markdown.
- User text remains plain text. Assistant/other content remains safe markdown.
- `User request:` parsing stays in `AgentConversationPresentationMapper`; the neutral project receives explicit visible and hidden text.
- Opaque `ConversationPresentationKey` values cross the neutral boundary; Agent `Guid` identities do not.
- Message order, pending-message placement, execution stream placement, and approval placement remain unchanged.
- Empty transcript copy, role labels, timestamps, token estimates, copy labels/values, avatar fallback, and pending status remain unchanged.
- Draft input, value-change callback, composer key, send callback, busy state, and disabled state remain explicit parameters.
- Prompt gallery, image/file attachments, voice actions, staged-attachment status, approval commands, cancellation, execution activity, runtime details, and title editing remain Agent-owned.
- No API client, SSE/polling code, LlmChats dependency, or Simple Chat feature was introduced.
- The legacy facade remains the only runtime consumer; neutral components have no backend side effects.
