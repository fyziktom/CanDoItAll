# API security and future chatbot readiness

## Scope policies

When API authorization is enabled, define and enforce:

- `llmchats.read` — list/read definitions, conversations, operations and events.
- `llmchats.manage` — create/update/archive definitions and conversations.
- `llmchats.execute` — admit turns and request cancellation/recovery.

Exact naming may follow existing scope conventions but must remain separate.

## Server-owned provenance

HTTP creates conversations with `Origin = Api`. Internal application calls use `Application`.
External callers cannot submit a trusted origin or internal ownership marker.

## External-client contract

- 202 admission returns operation ID, conversation ID, status URL, event URL and request fingerprint or
  idempotency representation.
- Status and SSE DTOs are versioned/stable and do not expose EF/domain internals.
- Error codes are stable and typed.
- Retry guidance distinguishes safe same-ID replay from creating a new operation.
- Rate-limit hooks may remain a later deployment concern, but contracts must not require UI cookies or
  Blazor circuit state.

## Future chatbot deployment

This bundle prepares the execution substrate only. A future `LlmChatDeployment` aggregate will own:

- public/embedded channel and endpoint;
- external participant/session identity;
- authentication/anonymous policy;
- moderation and PII rules;
- quotas/rate limits;
- retention/data residency/legal hold;
- human handoff;
- deployment-pinned definition revision.

None of those concepts should be stored as dormant nullable fields on reusable definitions or ordinary
internal conversations in this bundle.
