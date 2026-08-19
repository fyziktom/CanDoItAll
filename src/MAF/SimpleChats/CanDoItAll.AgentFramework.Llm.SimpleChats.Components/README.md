# CanDoItAll.AgentFramework.Llm.SimpleChats.Components

## Purpose

Blazor presentation and UI orchestration for Simple Chats, including definition management,
conversation workspaces, durable-operation following, floating conversation content, and typed UI
gateways to the application services.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework: `net10.0`
- Validation command:

```powershell
dotnet build src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Components/CanDoItAll.AgentFramework.Llm.SimpleChats.Components.csproj
```

## Boundaries

The project maps application contracts into presentation state and coordinates typed callbacks. It
contributes Simple Chats content to the backend-neutral conversation shell and uses the existing
CanDoItAll component libraries for rendered UI.

It does not own persistence, provider invocation, API routing, or domain transitions. Business rules
belong in Core and Application; runtime and persistence adapters belong outside the component project.

## Related Docs

- [LLM Chats Product And API](../../../../docs/llm-chats-api.md)
- [LLM Chats Boundary And Integration Ownership](../../../../docs/architecture/llm-chats-boundary-and-handoffs.md)
- [Conversation Shell](../../../UI/CanDoItAll.Conversations.Shell/README.md)
