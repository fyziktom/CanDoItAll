# CanDoItAll.Conversations.Shell

## Purpose

Backend-neutral floating conversation shell for conversation products. It owns the shell coordinator,
typed contributor contracts, host component, window state, and common shell actions while delegating
conversation content and lifecycle behavior to registered product contributors.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework: `net10.0`
- Validation command:

```powershell
dotnet build src/UI/CanDoItAll.Conversations.Shell/CanDoItAll.Conversations.Shell.csproj
```

## Boundaries

The shell may depend on the backend-neutral conversation presentation project and shared CanDoItAll
component libraries. Contributors supply opaque conversation keys, labels, rendered content, and typed
commands; the shell does not interpret product identifiers.

It does not own transcripts, persistence, provider execution, API access, authorization policy, or
product-specific mapping. Agent Chat and Simple Chats remain separate contributors with independent
application/runtime boundaries.

## Related Docs

- [Conversation Presentation Components](../CanDoItAll.Conversations.Components/README.md)
- [LLM Chats Boundary And Integration Ownership](../../../docs/architecture/llm-chats-boundary-and-handoffs.md)
