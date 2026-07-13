# Current State

## CodeAnalytics Evidence

- Snapshot: `snap-20260706235051-789dd62f`
- Scope: `CanDoItAll.AgentFramework.Maf`, `CanDoItAll.AgentFramework.Tools`, `CanDoItAll.AgentFramework.Tools.Abstractions`, `CanDoItAll.AgentFramework.Tooling`, `CanDoItAll.Tests.Unit`
- Findings: `WorkspaceRuntimePlugin.cs` is a 964-line hotspot with 89 members. `ToolCapabilityBuilder.cs` and `RuntimeCapabilityComposer.cs` are also broad construction points.
- Dashboard note: `code_analytics_dashboard_get` timed out, but snapshot build, findings, dependency, and symbol searches succeeded.

## Source Inventory

| Source | Observation |
|---|---|
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceRuntimePlugin.cs` | Owns filesystem, git, dotnet, scripts, conversion, spreadsheet inspection, image inspection, image analysis, and access checks. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/ToolCapabilityBuilder.cs` | Hard-coded switch maps capability keys to concrete plugin methods. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/ToolCapabilityBuilder.ConfiguredWorkspace.cs` | Hand-registers configured workspace tools by string name. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Files/WorkspaceFileContracts.cs` | `IWorkspaceFileService` already exposes `HashPath`, `ZipPath`, and `UnzipArchive`, but they are not agent tools. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Files/WorkspaceFileQueryService.cs` | `ListFiles` recurses; globstar exists but shallow folder listing is not explicit. |
| `repo://Templates/Capabilities/tools.json` | Capability templates include basic file tools but not hash, zip, unzip, or explicit directory listing. |

## Current Root Cause

The low-level file service has useful operations and safety checks, but the agent-visible tool surface is manually assembled in MAF runtime builders. That makes filesystem tools harder to discover, and adding commands currently means editing broad runtime capability code. The immediate architecture repair is to isolate filesystem runtime behavior into a cohesive plugin and expose missing existing service operations through typed constants, templates, and tests.
