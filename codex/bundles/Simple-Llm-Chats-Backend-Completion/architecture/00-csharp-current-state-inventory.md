# C# Current-State Inventory

## Analysis Scope

- Baseline commit: `a8e3f87e9ac917357c13fae56ab5eb1f0659521d`.
- CodeAnalytics snapshot: `snap-20260815201127-356b279c`.
- Scoped projects: Models, Providers, LLM Abstractions, LLM Conversations, LLM ProviderRuntime, Modules.LlmChats, Modules.LlmChats.Persistence, Composition, and Web.
- Result: 9 projects, 1,354 types, 9,546 members, 43 service registrations, 407 informational/complexity findings, 0 diagnostics, 0 open questions, and 0 dependency cycles.

## Existing Responsibility Owners

| Owner | Current responsibility | Current concern |
| --- | --- | --- |
| `CanDoItAll.AgentFramework.Models` | Provider/model typed facts | Stable shared dependency; no change planned |
| `CanDoItAll.AgentFramework.Providers` | Provider profiles/drivers and external protocol | Driver exception may retain raw provider body; keep inside boundary |
| `CanDoItAll.AgentFramework.Llm.Abstractions` | Provider-neutral invocation/messages/usage/stream updates | Completion finish reason is external-protocol text; remain bounded and provider-neutral |
| `CanDoItAll.AgentFramework.Llm.Conversations` | Generic transcript domain/service | Persists the system instruction as a system message; public filtering belongs to product read model |
| `CanDoItAll.AgentFramework.Llm.ProviderRuntime` | Concrete provider-neutral invocation adapters | Streaming adapter logs raw exception objects and needs typed consumer-abort outcome |
| `CanDoItAll.Modules.LlmChats` | Product domain, application services, ports, operation lifecycle, journal orchestration | Executor supervision, recovery, cancellation race, options, and transient maps |
| `CanDoItAll.Modules.LlmChats.Persistence` | EF/PostgreSQL implementations, runtime adapters, transfer | CAS, atomic definition pin, replay snapshot, retention, schema/transfer bounds |
| `CanDoItAll.Composition` | Product composition and hosted dispatch | Bounded worker hosting and option binding |
| `CanDoItAll.Web` | HTTP/SSE transport, auth metadata, DTOs, mapping | Mixed endpoint owner, validation/privacy/audit omissions |

## Complexity Findings Used

- `LlmChatsApi.cs` is approximately 466 lines and mixes two endpoint families; it has a concrete ownership split.
- `LlmChatConversationEngine.cs` is approximately 391 lines, but current analysis did not prove an independent second owner. Line count alone does not justify extraction.
- Large provider/conversation contract catalogs group related immutable contracts; do not split them only to satisfy a line threshold.
- `EfLlmChatOperationEventRepository` has multiple persistence responsibilities, but they share one operation-event repository boundary. Prefer small private/query helpers unless a real new port emerges.

## Current Composition/Lifetime Facts

- LLM Chat application services are scoped.
- Event signal and retention schedule are singleton process-local accelerators; PostgreSQL remains authoritative.
- The canonical provider resolver is scoped after `ec55926`.
- Database runtime leases fence profile identity/generation and now synchronize notification/disposal.
- Hosted dispatch owns provider execution; HTTP admission returns `202` and must not execute the provider inline.

## Inventory Stop Rule

If execution discovers a new project reference, public interface, partial type, Web-to-persistence dependency, or agent/runtime dependency not described here, stop the current subbundle and reopen the architecture checkpoint before continuing.
