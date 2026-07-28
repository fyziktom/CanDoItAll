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

## References

Architecture-relevant project references (the project file is the complete graph):

- `../../MAF/Common/CanDoItAll.AgentFramework.Components/CanDoItAll.AgentFramework.Components.csproj`
- `../../MAF/Common/CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj`
- `../../MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj`
- `../../MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `../../MAF/Common/CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj`
- `../../MAF/Common/CanDoItAll.AgentFramework.Persistence/CanDoItAll.AgentFramework.Persistence.csproj`
- `../../MAF/Tools/CanDoItAll.AgentFramework.Tooling/CanDoItAll.AgentFramework.Tooling.csproj`
- `../../Foundation/CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj`
- `../../Foundation/CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj`
- `../CanDoItAll.Modules.CrmHr/CanDoItAll.Modules.CrmHr.csproj`
- `../CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj`
- `../CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj`

Framework references:

- None

Direct package references:

- `Microsoft.AspNetCore.Components.Web (10.0.5)`
- `CanDoItAll.Components.BaseLib (0.1.4)`
- `CanDoItAll.Components.Charts (0.1.4)`
- `CanDoItAll.Components.OverlayLib (0.1.4)`

## Architecture Notes

This module owns product semantics for its bounded area. Keep business behavior here and expose it through typed services, Razor components, and module contracts. MCP projects should call into these services instead of duplicating module logic.

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
