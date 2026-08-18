# CanDoItAll.AgentFramework.Llm.SimpleChats.Application

## Purpose

Application contracts and orchestration for the Simple Chats product. The project coordinates chat
definitions, conversations, durable operations, streaming event journals, cancellation, recovery,
execution leases, retention, and profile-scoped access through explicit ports.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework: `net10.0`
- Validation command:

```powershell
dotnet build src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Application/CanDoItAll.AgentFramework.Llm.SimpleChats.Application.csproj
```

## Boundaries

This project owns use-case sequencing, request and response contracts, typed application errors, and
ports for persistence, provider resolution, dispatch, and execution evidence. The core project remains
the authority for domain state and transitions; persistence and provider SDK details are supplied by
outer adapters.

It does not reference Web endpoints, Razor components, EF Core, concrete provider drivers, or agent-run
execution. Provider calls must occur outside database transactions, while durable admission and
completion remain explicit application steps.

## Related Docs

- [LLM Chats Product And API](../../../../docs/llm-chats-api.md)
- [LLM Chats Boundary And Integration Ownership](../../../../docs/architecture/llm-chats-boundary-and-handoffs.md)
