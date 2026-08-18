# Stable error catalog

Suggested stable categories/codes:

| Code | HTTP | Meaning |
|---|---:|---|
| `llmchats.definition-not-found` | 404 | Definition absent in current profile/scope |
| `llmchats.definition-unavailable` | 409 | Definition suspended/archived for new work |
| `llmchats.conversation-not-found` | 404 | Conversation absent |
| `llmchats.conversation-archived` | 409 | New turn not allowed |
| `llmchats.conversation-busy` | 409 | Active turn/nonterminal operation exists |
| `llmchats.revision-conflict` | 409 | Expected revision stale |
| `llmchats.operation-not-found` | 404 | Operation absent |
| `llmchats.idempotency-conflict` | 409 | Same operation ID, different fingerprint |
| `llmchats.operation-lease-conflict` | 409 | Claim epoch/owner stale |
| `llmchats.operation-recovery-required` | 409 | Automatic continuation unsafe |
| `llmchats.profile-changed` | 409/410 | Original profile generation no longer active |
| `llmchats.provider-unavailable` | 503 | Provider/model not dispatchable |
| `llmchats.provider-failed` | 502/500 | Sanitized provider failure |
| `llmchats.deadline-exceeded` | 504 | Provider deadline |
| `llmchats.cancelled` | 409/200 snapshot | Durable cancellation outcome |
| `llmchats.stream-cursor-invalid` | 400 | Invalid Last-Event-ID/after |
| `llmchats.stream-gap` | SSE event | Retained range unavailable |
| `llmchats.stream-limit-exceeded` | terminal failure | Content/event/duration bound exceeded |
| `llmchats.scope-required` | 403 | Missing read/manage/execute scope |

Do not expose provider response bodies, URLs, credentials, prompt content, SQL messages or raw
exceptions in API errors.
