# C# Current State Inventory

## CodeAnalytics

- Snapshot id: `snap-20260706235051-789dd62f`
- Diagnostics: snapshot built without blocking errors. Dashboard call timed out. Existing `Microsoft.OpenApi` vulnerability warning appeared during project load.
- Relevant finding: `WorkspaceRuntimePlugin` has 89 members and owns unrelated workspace tool responsibilities.

## Large Classes And Partial Classes

| Type | Source | Current responsibility concern |
|---|---|---|
| `WorkspaceRuntimePlugin` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceRuntimePlugin.cs` | Filesystem, git, dotnet, scripts, document conversion, spreadsheet inspection, image inspection, image analysis, and access rules in one type. |
| `ToolCapabilityBuilder` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/ToolCapabilityBuilder.cs` | Maps many runtime tool keys to concrete method delegates with a large switch. |
| `ConfiguredWorkspaceToolSet` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/ToolCapabilityBuilder.ConfiguredWorkspace.cs` | Hand-registers configured workspace tools by string name and description. |

## Existing Filesystem Capability

- `IWorkspaceFileService` already exposes list, search, read, stat, hash, create directory, write, append, copy, move, delete, zip, unzip, and diff.
- `WorkspaceFileService` delegates query and mutation behavior to `WorkspaceFileQueryService` and `WorkspaceFileMutationService`.
- `WorkspacePathPolicy` handles workspace-root and external-target alias path resolution.
- `WorkspaceFileMutationService` already protects project surfaces and folder operations.

## Missing Tests

- No direct test for an extracted filesystem runtime plugin because it does not exist yet.
- Existing capability tests cover some workspace tool names, but not hash, zip, unzip, or explicit non-recursive listing.
