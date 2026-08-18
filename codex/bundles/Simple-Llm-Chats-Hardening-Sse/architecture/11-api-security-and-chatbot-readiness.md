# API security and future chatbot readiness

## Scope policies

When API authorization is enabled, define and enforce:

- `llmchats.read` — list/read definitions, conversations, operations and events.
- `llmchats.manage` — create/update/archive definitions and conversations.
- `llmchats.execute` — admit turns and request cancellation/recovery.

Exact naming may follow existing scope conventions but must remain separate.

SB10 uses `api.llm-chats.read`, `api.llm-chats.manage`, and `api.llm-chats.execute`. These are exact
scopes: the pre-existing broad `api` scope does not imply any LLM Chat permission. Endpoint policy
metadata is attached only when API authorization is enabled, preserving the trusted-local host mode.

## Server-owned provenance

HTTP creates conversations with `Origin = Api`. Internal application calls use `Application`.
External callers cannot submit a trusted origin or internal ownership marker.

The HTTP create-conversation request contains only the title and rejects unmapped `origin` input. The
Web transport supplies `Api` explicitly to the application command. Other trusted application callers
remain responsible for supplying `Application` at their composition boundary.

## External-client contract

- 202 admission returns operation ID, conversation ID, status URL, event URL and request fingerprint or
  idempotency representation.
- Status and SSE DTOs are versioned/stable and do not expose EF/domain internals.
- Error codes are stable and typed.
- Retry guidance distinguishes safe same-ID replay from creating a new operation.
- Rate-limit hooks may remain a later deployment concern, but contracts must not require UI cookies or
  Blazor circuit state.

Operation snapshots carry `candoitall.llm-chat-operation.v1`; SSE envelopes carry
`candoitall.llm-chat-operation-event.v1`. Definition responses omit the stored system prompt, failure
responses contain only stable product codes/identities, and unexpected executor logs record the
exception type rather than raw exception text or stack content.

## Conversation-create idempotency decision

Conversation creation remains explicitly non-idempotent in this bundle. A safe caller key cannot yet be
scoped without the external tenant, deployment, participant/session, or channel identity that belongs
to the future `LlmChatDeployment` boundary. A global or definition-local key would create cross-client
collisions and become a second identity model that later deployment work would have to migrate.

The OpenAPI operation therefore warns clients not to blindly retry an ambiguous create response. Turn
admission is the bounded retry-safe contract now: the caller supplies a validated operation ID and the
server rejects same-ID/different-fingerprint requests without returning either request body or
fingerprint. Deployment work may add a deployment-scoped conversation idempotency key when the missing
identity boundary exists.

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
