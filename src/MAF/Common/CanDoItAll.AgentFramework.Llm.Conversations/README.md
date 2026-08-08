# CanDoItAll.AgentFramework.Llm.Conversations

## Purpose

Application-level foundation for ordinary multi-turn LLM conversations. Owns the canonical
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
They must not be composed into one implicit runtime.

Turn admission reserves capacity for both the user and assistant entries before calling a provider.
The optimistic revision compare-and-swap persists the pending user entry and an active-turn marker with
the exact turn id, entry id, admitted revision, timestamp, and compensation state. Completion replaces
that marker with the assistant entry. Provider failure, cancellation, or explicit
`AbandonActiveTurnAsync` removes only the admitted pending entry, restores the provider and acceleration
snapshot, clears the marker, and advances the revision. Rename is rejected while a turn is active;
delete remains terminal.

The file store uses schema version 2. Idle version-1 documents remain readable, but an active legacy
turn without compensation metadata fails with a typed storage-corruption result instead of guessing a
rollback. A canonical-path, process-wide reference-counted coordinator serializes compare-and-swap
operations across store instances, bounds coordinator state, and cleans temporary files after atomic
replacement.

`LlmUsage` is immutable, rejects negative counters, and aggregates attempts with checked arithmetic.
Provider metrics from every completed attempt contribute to the result; typed invocation failures carry
known prior-attempt usage. Invalid or overflowing provider counters are sanitized to a provider failure,
and workflow projections preserve known failure usage.

## Production Activation

The ordinary conversation library is opt-in and is not registered by the current product composition
root. Activating it requires an explicit product API/UI, retention policy, integration tests, storage
root ownership, and provider resolution fenced by the current database profile id and generation.
Profile switching must invalidate active work or resolve the provider per operation; a singleton or
unfenced registration is not supported.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
- Process and MAF authority boundary: `docs/architecture/process-maf-1.15-outcome-authority.md`
