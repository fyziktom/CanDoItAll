# SB15 production activation decision

The ordinary LLM conversation foundation is useful, but no product UI or API consumes it yet.

The current app registration resolves a file root once for a scoped service. Long-lived Blazor scopes can
outlive a database-profile switch, so future callers could continue writing conversations under the old
profile root.

## Merge-safe decision for this bundle

Keep:

- `CanDoItAll.AgentFramework.Llm.Abstractions`;
- `CanDoItAll.AgentFramework.Llm.Conversations`;
- file and in-memory stores;
- service contracts;
- unit tests;
- solution references.

Remove from current production module composition:

```text
services.AddLlmConversations(...)
```

Add an architecture test proving that no App or product module registers or consumes
`ILlmConversationService` yet.

A future feature subbundle may activate it only with:

- explicit current-profile identity;
- database-profile generation fencing;
- profile-switch invalidation or per-operation resolution;
- a product-owned API/UI;
- usage and retention policy;
- integration tests across profile switching.

This is deactivation, not deletion and not a new feature.
