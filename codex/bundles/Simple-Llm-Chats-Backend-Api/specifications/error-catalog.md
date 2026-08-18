# Stable error catalog

The exact prefix may be aligned to the repository convention at SB00. Semantics and HTTP classes are
locked.

| Code | Typical HTTP | Meaning |
|---|---:|---|
| `llm-chat.invalid-request` | 400 | malformed or out-of-bounds request |
| `llm-chat.definition-not-found` | 404 | definition does not exist in current scope |
| `llm-chat.definition-concurrency-conflict` | 409 | stale definition token |
| `llm-chat.definition-not-active` | 409 | Draft/Suspended/Archived blocks requested action |
| `llm-chat.conversation-not-found` | 404 | conversation does not exist in current scope |
| `llm-chat.conversation-archived` | 409 | conversation is read-only |
| `llm-chat.transcript-revision-conflict` | 409 | stale expected transcript revision |
| `llm-chat.active-turn-conflict` | 409 | another turn is active |
| `llm-chat.operation-not-found` | 404 | operation does not exist |
| `llm-chat.operation-id-conflict` | 409 | same operation ID carries a different request fingerprint |
| `llm-chat.operation-recovery-required` | 409 | exact active-turn recovery is required |
| `llm-chat.provider-not-found` | 422 | referenced provider no longer exists |
| `llm-chat.provider-kind-mismatch` | 422 | live provider kind differs from revision snapshot |
| `llm-chat.model-not-supported` | 422 | selected model is invalid for live provider |
| `llm-chat.model-settings-invalid` | 422 | temperature/parameters/format are unsupported |
| `llm-chat.thinking-effort-not-supported` | 422 | selected provider/model does not support the explicit effort |
| `llm-chat.runtime-profile-changed` | 409/410 | profile identity changed during operation |
| `llm-chat.cancelled` | 409/499-equivalent convention | operation cancelled |
| `llm-chat.deadline-exceeded` | 504 | provider deadline exceeded |
| `llm-chat.provider-unavailable` | 503 | provider/runtime failure |
| `llm-chat.storage-conflict` | 409 | database CAS or concurrency conflict |
| `llm-chat.storage-corrupted` | 500 | persisted transcript invariant failed |
| `llm-chat.unsupported-context` | 422 | context input is deferred/not registered |
| `llm-chat.unsupported-attachment` | 422 | attachment input is deferred/not registered |
| `llm-chat.external-origin-not-supported` | 422 | no deployment/channel adapter owns the origin |

Public ProblemDetails may include:

- code;
- operation ID;
- definition/conversation ID;
- retryability;
- expected/current revision where safe.

It must not include prompts, system instructions, provider payloads, credentials, connection strings,
or raw exception text.
