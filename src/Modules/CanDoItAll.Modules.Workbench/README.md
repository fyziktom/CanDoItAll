# CanDoItAll.Modules.Workbench

## Purpose

Product module for workbench views, projections, canvas state, and user workspace orchestration.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj
```

## References

Architecture-relevant project references (the project file is the complete graph):

- `../../MAF/Common/CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj`
- `../../MAF/Common/CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj`
- `../../MAF/Common/CanDoItAll.AgentFramework.Components/CanDoItAll.AgentFramework.Components.csproj`
- `../../MAF/Tools/CanDoItAll.AgentFramework.Tooling/CanDoItAll.AgentFramework.Tooling.csproj`
- `../../Foundation/CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj`
- `../../Foundation/CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj`
- `../../Processes/CanDoItAll.Processes.Application/CanDoItAll.Processes.Application.csproj`
- `../../Processes/CanDoItAll.Processes.Persistence/CanDoItAll.Processes.Persistence.csproj`
- `../CanDoItAll.Modules.Projects/CanDoItAll.Modules.Projects.csproj`
- `../CanDoItAll.Modules.Resources/CanDoItAll.Modules.Resources.csproj`
- `../CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj`

Framework references:

- None

Direct package references:

- `Microsoft.AspNetCore.Components.Web (10.0.5)`
- `Microsoft.Extensions.Http (10.0.0)`
- `CanDoItAll.Components.BaseLib (0.1.4)`
- `CanDoItAll.Components.CanvasLib (0.1.4)`
- `CanDoItAll.Components.Gantt (0.1.4)`
- `CanDoItAll.Components.Mermaid (0.1.4)`
- `CanDoItAll.FileTools.Desktop (0.1.2)`
- `CanDoItAll.FileTools.FileBrowser.Components (0.2.0)`
- `CanDoItAll.FileTools.FileBrowser.Core (0.1.0)`
- `CanDoItAll.FileTools.FileInteraction.Components (0.2.1)`
- `CanDoItAll.FileTools.FileInteraction.Core (0.1.0)`
- `CanDoItAll.FileTools.FileInteraction.Markdown (0.1.2)`

## Architecture Notes

This module owns product semantics for its bounded area. Keep business behavior here and expose it through typed services, Razor components, and module contracts. MCP projects should call into these services instead of duplicating module logic.

Processes.Application owns process-run root semantics through `ProcessRunArtifactRootPolicy`. Workbench consumes its typed resolution when projecting current-run managed roots, collapses artifact evidence under `artifacts/.../process-runs/{runId}` to the run artifact folder, and collapses generated or external-delivery output persisted under `output/.../process-runs/{runId}/{productRoot}` to the product folder. Wrong-run, dated receipt, absolute, traversal, or otherwise unanchored paths are ignored instead of mirroring noisy artifact subtrees. Raw `external-target/...` aliases remain Processes grounding metadata; Workbench projects the managed output root that records the run-owned delivery evidence.

### Project Structure Agent Invocation Snapshot

The ready Project Structure chat-context provider publishes a typed
`ProjectStructureInvocationSnapshot` copied from the surface already loaded by the UI.
It retains no component, tracked entity, service, or mutable domain object. The
snapshot is bounded to 512 nodes and 1,024 links, includes explicit coverage and
omissions, carries database-profile generation plus deterministic fingerprints, and
expires after five minutes.

`ProjectStructureReadRequest.Source` is a typed three-way policy:

- `ContextDefault` selects the invocation snapshot only for eligible interactive
  Project Structure chat and otherwise selects canonical current data.
- `InvocationSnapshot` requires that exact held snapshot and fails closed on
  context/scope/project/profile/freshness/fingerprint/coverage mismatch.
- `CanonicalCurrent` performs the canonical service read.

There is no silent snapshot-to-database fallback. Governed process execution and
non-Project Structure contexts use canonical data. Snapshot reads are read-only
context; all mutations still pass through current canonical authorization and
concurrency checks.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
- Agent execution activity and runtime snapshots: `docs/architecture/agent-execution-activity-and-runtime-snapshots.md`
- Agent runtime tool surface: `docs/agent-runtime-tool-surface.md`
