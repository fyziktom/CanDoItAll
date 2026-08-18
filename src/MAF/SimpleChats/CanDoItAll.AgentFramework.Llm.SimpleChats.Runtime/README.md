# CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime

## Purpose

Runtime composition between the Simple Chats application boundary and the canonical LLM provider
runtime. The project resolves provider/model profiles, constructs scoped conversation engines, invokes
providers, and captures auditable invocation evidence.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework: `net10.0`
- Validation command:

```powershell
dotnet build src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime/CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime.csproj
```

## Boundaries

The project implements runtime-facing Application ports and composes the generic ordinary-conversation
service inside a scoped Simple Chats engine. Provider selection is fenced by the active database
profile and runtime generation, and invocation evidence is captured for both successful and failed
attempts.

It does not own HTTP endpoints, UI, EF persistence, definition policy, or durable operation state. It
must not expose the generic conversation service globally or reuse full agent execution as a chat
runtime.

## Related Docs

- [LLM Chats Product And API](../../../../docs/llm-chats-api.md)
- [LLM Chats Boundary And Integration Ownership](../../../../docs/architecture/llm-chats-boundary-and-handoffs.md)
- [Provider Runtime](../../Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/README.md)
