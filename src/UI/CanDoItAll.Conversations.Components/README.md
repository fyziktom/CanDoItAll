# CanDoItAll.Conversations.Components

## Purpose

Backend-neutral Blazor presentation components and immutable presentation contracts for conversation-oriented application UI.

## Project type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework: `net10.0`
- Validation command:

```powershell
dotnet build src/UI/CanDoItAll.Conversations.Components/CanDoItAll.Conversations.Components.csproj
```

## Architecture notes

This project renders source-neutral presentation state and raises typed callbacks. It does not own persistence, provider/runtime execution, lifecycle orchestration, service discovery, or product-specific mapping.

Allowed dependencies are Blazor and the shared CanDoItAll component libraries required by rendered UI. AgentFramework, product modules, backend services, EF Core, persistence, and provider SDKs must not be referenced.

Product owners adapt their domain/runtime state into the contracts in this project. Opaque keys are never interpreted here.

## Related docs

- Repository architecture: `docs/architecture/overview.md`
- Conversation components boundary: `docs/architecture/conversation-components-boundary.md`
