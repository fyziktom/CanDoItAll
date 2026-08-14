# Current state and delta

## What exists

The repository already has a technically strong ordinary-conversation library. The missing work is
product activation, not a rewrite of provider dispatch.

```text
Workflows or future product
        |
        v
ILlmInvocationPort  <---- provider runtime pool / drivers
        ^
        |
LlmConversationService
        |
  in-memory/file stores
```

The existing service is intentionally independent from agent execution and already handles transcript
revision, active-turn admission, provider switch policy, and compensation.

## What is missing

```text
LlmChatDefinition + immutable revisions
LlmChat product application service
PostgreSQL ILlmConversationStore
product conversation metadata
operation/idempotency/cancellation/audit
database-profile generation fence
HTTP API
focused production integration tests
```

## Target delta

```text
Web API
   |
   v
CanDoItAll.Modules.LlmChats
   |             |           -> existing ILlmInvocationPort
   v
CanDoItAll.Modules.LlmChats.Persistence
   |
   +-> AppDbContext / PostgreSQL
   +-> existing database runtime profile identity

Product-owned LlmChatConversationEngine
   |
   +-> existing LlmConversationService
   +-> EF ILlmConversationStore
   +-> profile-fenced product invocation adapter
```

## Why the generic global registration stays off

`AddLlmConversations` binds the generic service to a storage-root resolver and is appropriate for
isolated file-store composition. The production product needs:

- PostgreSQL;
- definition metadata;
- profile generation fencing;
- operation scope and audit;
- server-side provider resolution.

Registering the generic service globally would make those invariants optional. The product module
therefore exposes its own named engine and does not publish a general production
`ILlmConversationService`.

## SB00 execution re-anchor (2026-08-14)

- active branch: `simple-chats`;
- HEAD: `c0117109c6ef6166d1d8b1b42d75e7f4af83c5ee` (the prepared commit);
- working tree before execution: only this untracked bundle;
- CodeAnalytics architecture snapshot: `snap-20260814151553-cf742c1c`, scoped to eight
  LLM/provider/infrastructure/composition/Web projects, 384 documents, 1,363 types, 9,887 members,
  no blocking diagnostics, and no project-reference cycle in the scoped graph;
- CodeAnalytics thinking-effort snapshot: `snap-20260814152917-c5b941c8`, scoped to
  `CanDoItAll.AgentFramework.Models`, 651 types, 4,929 members, and no diagnostics;
- prepared source paths still exist;
- the latest migration is `20260813012618_CorrectProcessPlanHashClassification`; the immutable
  `20260728161028_InitialPostgreSqlBaseline` remains unchanged and a new migration must append after
  the current latest migration.

The scoped CodeAnalytics result reports pre-existing module/type cycles inside Infrastructure/Core,
but no project-reference cycle across the affected project slice. Those pre-existing internal cycles
are not on the planned LLM Chats dependency path and must not be widened.
