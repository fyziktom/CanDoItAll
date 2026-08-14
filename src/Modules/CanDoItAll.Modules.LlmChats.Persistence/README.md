# CanDoItAll.Modules.LlmChats.Persistence

## Purpose

PostgreSQL, canonical ordinary-conversation, provider-runtime, database-profile fencing, cancellation,
and database-transfer adapters for `CanDoItAll.Modules.LlmChats`.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework: `net10.0`
- Focused build:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.LlmChats.Persistence/CanDoItAll.Modules.LlmChats.Persistence.csproj --configuration Release
```

## Boundaries

The project owns EF configurations/repositories, unit of work, the PostgreSQL `ILlmConversationStore`,
provider/model resolution through the canonical runtime profile source, the product conversation engine,
runtime generation leases, operation cancellation, and complete database-transfer participation.

It does not reference Web/Razor, MAF, tools, skills, MCP, memory, processes, Workbench, or other product
UI implementations. The generic ordinary-conversation service is constructed only inside the scoped
product engine; it is not globally published. The file conversation store is not registered in
production. See [LLM Chats Backend API](../../../docs/llm-chats-api.md).
