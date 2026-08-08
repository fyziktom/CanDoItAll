# CanDoItAll.AgentFramework.Llm.Conversations

## Purpose

Application-level foundation for ordinary multi-turn LLM conversations (SB15). Owns the canonical
conversation transcript, atomic turn admission/completion, provider/model snapshot and explicit switch
policy, bounded non-destructive context-window selection, and durable file-backed persistence. Every
inference call delegates to the stateless `ILlmInvocationPort`; provider conversation state is at most
an opaque acceleration envelope, never the source of truth.

This is deliberately **not** agent execution: no tools, memory, agent catalog, workspace authority,
approvals, finalizers, handoffs, or process semantics exist here, and the project must never reference
agent runtime, MAF, provider driver, or module projects.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/MAF/Common/CanDoItAll.AgentFramework.Llm.Conversations/CanDoItAll.AgentFramework.Llm.Conversations.csproj
```

## Dependencies

The authoritative project and package dependency list is in
[CanDoItAll.AgentFramework.Llm.Conversations.csproj](CanDoItAll.AgentFramework.Llm.Conversations.csproj).
Allowed references are `CanDoItAll.AgentFramework.Llm.Abstractions` and
`CanDoItAll.AgentFramework.Models` only; the lightweight-path guard tests enforce this.

## Architecture Notes

Three separate products share the lightweight LLM family: stateless invocation (`ILlmInvocationPort`),
ordinary conversation (`ILlmConversationService`, this project), and full agent execution (elsewhere).
Concurrent turns cannot corrupt transcript order: a turn is admitted via an optimistic revision
compare-and-swap that persists the pending user entry plus an in-flight marker, and a failed turn rolls
back to the pre-turn transcript. Crash recovery is explicit (`AbandonActiveTurnAsync`), never heuristic.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
