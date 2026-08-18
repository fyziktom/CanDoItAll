# C# Dependency Direction

## Allowed Project Direction

| Project | May depend on |
| --- | --- |
| AgentFramework.Models | none in this scope |
| AgentFramework.Providers | Models |
| AgentFramework.Llm.Abstractions | Models |
| AgentFramework.Llm.Conversations | Llm.Abstractions, Models |
| AgentFramework.Llm.ProviderRuntime | Llm.Abstractions, Models, Providers |
| Modules.LlmChats | Llm.Abstractions, Models, SharedKernel/standard DI-logging abstractions |
| Modules.LlmChats.Persistence | Modules.LlmChats, Llm.Abstractions, Llm.Conversations, Llm.ProviderRuntime, Providers, Infrastructure persistence/control plane |
| Composition | Modules.LlmChats.Persistence and existing composition dependencies |
| Web | Modules.LlmChats and Composition; never Modules.LlmChats.Persistence |

## Forbidden Edges

- Core -> Persistence, EF Core, Npgsql, ASP.NET, Web, Razor, Composition, Agent Core/MAF execution, tools, memory, workflows, processes, or project/workspace modules.
- Persistence -> Web/UI.
- ProviderRuntime/Providers -> Modules.LlmChats.
- Web -> `AppDbContext` or concrete LLM Chat repositories.
- Any new cycle or new test-only production reference.

## Change Rules

- New schema fields are represented in core only when they are domain facts (finish reason, delivery mode, high-water); EF mapping stays in Persistence.
- Stable HTTP error translation consumes typed core failures. Do not catch broad exceptions in Web to fake 409/400.
- CAS failures are translated at the repository/application boundary where database concurrency meaning is known.
- Worker fan-out calls the existing dispatcher/application services; it does not bypass leases or repositories.
- Process-local signal eviction may reduce wake latency but can never become correctness authority.

## Architecture Proof

- Capture project-reference graph before and after the affected union.
- Run CodeAnalytics cycle/reference queries and repository architecture guards.
- Source assertions must prove no Web reference to Persistence/EF, no inline provider dispatch in HTTP, no globally activated file store, no partial-type split, and no new agent execution dependency.
- Any intended dependency change requires a new pattern/decision record and re-entry through `plan/architecture-checkpoints.md`.
