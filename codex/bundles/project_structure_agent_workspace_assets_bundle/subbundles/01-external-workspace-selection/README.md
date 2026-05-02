# External Workspace Selection

## Status

- `Completed`

## Objective

Allow technical agents to be granted explicit external filesystem workspace roots and enforce those roots at runtime for browse/search/read and write-capable workspace tools.

## Covered Inputs

- `NOTE-01`
- `NOTE-04` for external drive browse/search/read behavior

## Prerequisites

- Prepared bundle validation passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Agents\Access\AgentProjectStructureAccessModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Editors\EditorModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Catalog\AgentFrameworkWorkspaceCatalogService.Agents.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Paths\WorkspacePathPolicy.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workspace\MafAgentRuntime.WorkspaceRuntimePlugin.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.Tools.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCatalogPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCatalogPanel.razor.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\WorkspaceExternalTargetAliasTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\WorkspaceFileQueryServiceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\MafAgentRuntimeTests.cs`

## Deliverables

- New per-agent workspace/file access metadata for read/write and allowed external roots.
- Agent editor controls for entering selected external workspace roots.
- Normalization from absolute Windows paths to `external-target/<drive>/...` aliases.
- Runtime guards that deny unconfigured external aliases and write attempts when the agent is read-only.
- Focused tests for metadata round trip and external alias enforcement.

## Dependency Impact

- Critical foundation for storage/file tool defaults.
- If external alias enforcement is wrong, later proof that agents can analyze external repositories is untrustworthy.

## Validation Depth

- Critical foundation.
- Unit tests for normalization.
- Runtime/tool tests for allowed and denied external alias behavior.

## Implementation Steps

1. Add a typed workspace/file access settings model and metadata serializer.
2. Add the settings model to `AgentEditorModel` and persist it in `SaveAgentAsync`.
3. Extend the agent editor with read/write checkboxes and external-root entry.
4. Add reusable alias normalization helpers using the existing `WorkspacePathPolicy` behavior.
5. Wrap workspace file runtime operations with access checks before delegating to the file service.
6. Add tests for round-trip, absolute path normalization, allowed external alias access, and denied sibling/parent aliases.

## Scope Exceptions

- External root folder picker dialog is not required in this first pass; a text entry/textarea with one root per line is acceptable.
- Cross-platform alias display beyond Windows drive aliases is not required.

## Do Not Do

- Do not grant `external-target/C` or parent directories automatically.
- Do not bypass existing process-run external target metadata policies.
- Do not replace `WorkspacePathPolicy`.

## Acceptance Checklist

- Agent settings can store selected external workspace roots.
- Absolute paths and aliases normalize consistently.
- Agents with read access can list/search/read within selected aliases.
- Agents without write access cannot write external files.
- Agents cannot reach sibling external aliases outside the selected root.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter AgentWorkspaceToolAccessMetadataTests --no-restore -m:1` passed.
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter CreateCapabilityState_attaches_configured --no-restore -m:1` passed.
- Metadata normalization rejects drive roots, normalizes absolute Windows paths, and accepts only selected external alias roots and children.
- Runtime composition attaches native browse/search/read/stat tools for configured external roots without requiring shell workarounds.

## Browser Validation Logging

- N/A unless the editor UI layout changes in a way that needs visual proof.

## Progression Gate

- Downstream work may continue only after the guard tests prove that selected external roots are usable and unselected roots are denied.

## Suggested Agent Prompt

```text
Implement subbundle 01 only: add per-agent external workspace root settings and enforce them in workspace file tools. Preserve existing project/process access behavior and prove allowed/denied alias behavior with tests.
```
