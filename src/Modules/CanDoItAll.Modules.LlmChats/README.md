# CanDoItAll.Modules.LlmChats

## Purpose

Application and domain boundary for reusable LLM Chat definitions, immutable revisions, pinned
conversations, durable turn operations, execution leases, reconciliation, cancellation, streaming
events, and invocation audit.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework: `net10.0`
- Focused build:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.LlmChats/CanDoItAll.Modules.LlmChats.csproj --configuration Release
```

## Boundaries

The project exposes typed application services and persistence/runtime ports. It references lightweight
LLM/model contracts and the shared kernel only. It does not reference ASP.NET Core, EF Core, Razor,
AgentFramework Core/MAF, tools, skills, MCP, memory, processes, or product UI modules.

Thinking effort uses the canonical provider/model contracts. A nullable setting means provider default;
explicit `None` disables reasoning only when supported by the selected model. Unsupported effort is an
explicit error. No duplicate provider catalog or effort enum belongs here.

Persistence and provider invocation are implemented by `CanDoItAll.Modules.LlmChats.Persistence`.
Hosted dispatcher lifetime belongs to `CanDoItAll.Composition`; HTTP and SSE transport belong to
`CanDoItAll.Web`. Turn admission persists canonical state and signals dispatch but never awaits provider
I/O. The operation journal is the replay authority; transient event signals only wake readers after a
commit.

No LLM Chat definition or conversation carries dormant tenant, participant, channel, moderation, quota,
retention, residency, legal-hold, or human-handoff fields. Those concerns belong to a later deployment
aggregate. See [LLM Chats Backend API](../../../docs/llm-chats-api.md) and
[LLM Chats architecture and future handoffs](../../../docs/architecture/llm-chats-boundary-and-handoffs.md).
