# CanDoItAll.AgentFramework.Llm.SimpleChats.Core

## Purpose

Domain model for Simple Chats definitions, revisions, conversations, durable operations, invocation
evidence, identifiers, validation, fingerprints, paging, and legal operation-state transitions.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework: `net10.0`
- Validation command:

```powershell
dotnet build src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Core/CanDoItAll.AgentFramework.Llm.SimpleChats.Core.csproj
```

## Boundaries

This project is the Simple Chats domain authority. State transitions and invariants must remain typed,
deterministic, and independent of transport or storage concerns. It may depend on the shared kernel and
lightweight LLM contracts, but not on Application, Runtime, Persistence, Web, Razor, EF Core, or provider
SDKs.

Simple Chats is an ordinary LLM conversation product. The domain must not acquire agent tools, memory,
approvals, handoffs, workspace authority, or full agent-run semantics.

## Related Docs

- [LLM Chats Product And API](../../../../docs/llm-chats-api.md)
- [LLM Chats Boundary And Integration Ownership](../../../../docs/architecture/llm-chats-boundary-and-handoffs.md)
