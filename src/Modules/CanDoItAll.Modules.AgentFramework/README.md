# CanDoItAll.Modules.AgentFramework

## Purpose

Product module that exposes AgentFramework catalog, provider, execution, and technical-agent bridge capabilities to the app runtime.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Modules.AgentFramework.csproj](CanDoItAll.Modules.AgentFramework.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

This module owns product semantics for its bounded area. Keep business behavior here and expose it through typed services, Razor components, and module contracts. UI and transport adapters should call into these services instead of duplicating module logic.

The module adapts the generic activity/preparation contracts to the current database
profile:

- `AgentChatExecutionOrchestrator` admits an activity operation and returns its stream
  handle before awaiting context capture or execution.
- `CurrentProfileAgentExecutionActivityReader` authorizes database profile, generation,
  and organization workspace scope and cancels readers when that profile lifetime
  changes.
- `AgentChatPreparationPool` is circuit-scoped metadata preparation for active agent
  definitions only.
- `AgentExecutionPreparationCache` is scoped immutable execution preparation.
- `CanonicalProviderRuntimeProfileSnapshotService` is a singleton immutable provider
  projection fenced by database profile identity/generation and persistent provider
  concurrency revisions.

Provider database rows remain canonical. Save/delete commit observers update the
runtime projection after commit; a projection failure faults the snapshot explicitly
without hiding or reversing the canonical commit. Use-time revision probes either
confirm the immutable lease, refresh the changed provider, or fail closed.

Resolved secret values are not stored in the provider snapshot or preparation cache.
They are prepared for one execution dispatch, checked against the provider
configuration fingerprint, and cleared on scope disposal. Live MAF runtimes remain
per execution.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
- Agent execution activity and runtime snapshots: `docs/architecture/agent-execution-activity-and-runtime-snapshots.md`
- Reusable floating agent chats: `docs/architecture/reusable-floating-agent-chats.md`
