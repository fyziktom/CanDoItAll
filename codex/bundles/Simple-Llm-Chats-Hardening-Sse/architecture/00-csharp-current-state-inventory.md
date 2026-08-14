# C# current-state inventory

## Product core

`CanDoItAll.Modules.LlmChats` owns:

- definition identity, mutable head, immutable revisions, status, tags and settings;
- conversation binding to a pinned definition revision;
- operation identity, request fingerprint, state, cancellation and terminal metadata;
- application contracts and services.

This is the correct product owner. It must remain free of Web/Razor, EF Core, MAF agent execution,
tools, skills, MCP, memory and process dependencies.

## Persistence/runtime adapter

`CanDoItAll.Modules.LlmChats.Persistence` owns:

- EF entities and configurations;
- application repositories;
- the current EF implementation of the generic LLM conversation store;
- provider resolution and profile generation integration;
- invocation audit and database transfer.

The project currently mixes three responsibilities that need clearer seams:

1. canonical product command persistence;
2. read/query projections;
3. provider/runtime adapters.

SB01–SB05 may keep one assembly if dependency direction and cohesive types remain clear, but must not
solve this by adding one large partial service.

## Generic LLM foundation

- `Llm.Abstractions` owns stateless provider-neutral message/invocation contracts.
- `Llm.Conversations` owns generic conversation policy and file/EF store contracts.
- `Llm.ProviderRuntime` adapts provider runtime dispatch.
- `AgentFramework.Providers` owns provider drivers, capability contracts and dispatch infrastructure.

Streaming contracts belong alongside the existing provider-neutral invocation contracts, while concrete
wire parsing belongs in provider drivers.

## Web transport

The current API owns DTO mapping and HTTP status translation. It must remain thin. Durable execution,
events, recovery, idempotency and cancellation are application/runtime behavior, not endpoint behavior.

## Existing generic SSE

The Web API already owns transport-level replay writer and a profile-bounded event stream. The hardening
may generalize small reusable capabilities if necessary, but must not move product event truth into Web.

## Hotspots requiring shrink or replacement

- `LlmChatConversationApplicationService`: remove transaction choreography once command store owns it.
- `LlmChatOperationApplicationService`: split admission/query/cancel/recovery orchestration into cohesive
  services or handlers; no partial-class expansion.
- `EfLlmConversationStore`: remove independent command contexts or reduce to a read/compatibility adapter.
- `AuditedLlmChatInvocationPort`: replace post-hoc approximate audit with actual dispatch-attempt events.
- `LlmChatOperationsApi`: stop awaiting inference inline; become admission/status/cancel transport.
