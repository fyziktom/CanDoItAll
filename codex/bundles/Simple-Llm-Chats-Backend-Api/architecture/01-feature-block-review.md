# Feature-block architecture review

## Responsibility map

| Responsibility | Owner after this bundle |
|---|---|
| lightweight messages, settings, usage, typed invocation failure | `Llm.Abstractions` |
| provider/model thinking-effort levels, capability policy, and provider defaults | existing `AgentFramework.Models` provider-neutral contracts; no LLM Chat copy |
| provider-backed stateless dispatch and reusable invocation-port DI registration | `Llm.ProviderRuntime` |
| generic transcript transaction and compensation | `Llm.Conversations` |
| reusable product definition and revision | `Modules.LlmChats` |
| product conversation metadata and operation lifecycle | `Modules.LlmChats` |
| EF entities/configurations/repositories/unit of work | `Modules.LlmChats.Persistence` |
| profile-generation fence implementation | `Modules.LlmChats.Persistence` |
| HTTP transport and ProblemDetails mapping | `CanDoItAll.Web` |
| EF migration asset | `Migrations.PostgreSql` |
| future Project Structure context source | Workbench, later bundle |
| future common/UI chat components | UI/shared components, later bundle |
| future channel deployment | dedicated deployment/channel module, later bundle |

## Forces

- Reusable definitions and immutable revisions are product concepts, not transcript concepts.
- PostgreSQL persistence must be cross-process safe.
- Provider calls must be fenced by the current database profile.
- API retries must not duplicate paid work.
- Existing workflows must not acquire product-module dependencies.
- Future UI and external channels need stable product contracts without agent runtime types.

## Selected patterns

| Force | Pattern | Why |
|---|---|---|
| mutable definition with historical behavior | aggregate + append-only revision snapshot | existing threads and deployments stay deterministic |
| provider models expose different thinking efforts | reuse canonical per-model capability policy + typed nullable revision override | avoids capability drift; `null` is provider default and `None` is explicit disable |
| provider/profile resolution per operation | module port backed by provider-neutral read/capability contracts | avoids Core/module coupling and stale scoped identity |
| multiple future context sources | document the future strategy boundary only | no current consumer, so no registry or unused interface is created |
| paid command retry | persistent idempotent operation | survives process restart |
| crash gap between transcript and operation row | explicit saga reconciliation by shared turn/operation ID | does not pretend two internal commits are atomic |
| profile change during inference | scoped invocation decorator + store fence | blocks dispatch and commit |
| production and test transcript stores | `ILlmConversationStore` implementations | existing boundary already exists |

## Rejected approaches

- Agent with tools disabled.
- A second chat-session model copied from agent sessions.
- JSON files as the production API catalog.
- One large `LlmChatManager`.
- Global `IServiceProvider` lookups.
- Raw provider configuration JSON accepted from HTTP.
- A second LLM-Chat-only thinking-effort enum or capability catalog.
- UI-first implementation.
- One giant bundle with the full test suite after every change.
