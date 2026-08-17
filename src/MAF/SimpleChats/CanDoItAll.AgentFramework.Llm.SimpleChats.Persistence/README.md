# CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence

## Purpose

PostgreSQL, canonical ordinary-conversation, provider-runtime, database-profile fencing, execution
leases, durable stream events, cancellation, immutable pricing evidence, and database-transfer
adapters for the MAF Simple Chats application boundary.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework: `net10.0`
- Focused build:

```powershell
dotnet build src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.csproj --configuration Release
```

## Boundaries

The project owns EF configurations/repositories, the shared-context LLM Chat unit of work, the
PostgreSQL `ILlmConversationStore`, provider/model resolution through the canonical runtime profile
source, the product conversation engine, runtime-generation and execution-lease adapters, durable event
journal storage, retention, operation cancellation, and complete database-transfer participation. The
conversation store uses the same scoped `AppDbContext` as the owning unit of work; it must not create an
independent context for canonical transcript mutations. Provider I/O runs after admission commit and
outside database transactions.

It does not reference Web/Razor, Agent module execution or UI, tools, skills, MCP, memory, processes,
Workbench, or other product UI implementations. The generic ordinary-conversation service is
constructed only inside the scoped runtime engine; it is not globally published. The file conversation
store is not registered in production. See [LLM Chats Backend API](../../../../docs/llm-chats-api.md).

The nine `LlmChats_*` tables and their model snapshot are advanced by the append-only PostgreSQL
migration chain beginning with `20260814163458_AddLlmChats`; pricing evidence was added by
`20260817183339_AddSimpleChatInvocationPricingEvidence`. Migration bootstrap, pending-model validation, event
retention, and database transfer must stay aligned whenever this persistence boundary changes.
