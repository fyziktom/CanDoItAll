# Assumptions And Risks

## Assumptions

- `IWorkspaceFileService` and `WorkspacePathPolicy` remain the source of truth for physical path resolution and allowed-area enforcement.
- New filesystem tools use existing workspace profile flags: reads require `CanReadFiles`; mutations require `CanWriteFiles` or `CanManageWorkspacePaths`.
- Capability template seed version must be bumped when new tool capabilities are added.

## Critical Path Risks

- Duplicate tool names can occur if a new provider emits names already attached by configured workspace tools.
- Moving too much at once can destabilize git/dotnet/script/document/image paths outside this request.
- Archive extraction must keep zip-slip protection from `WorkspaceFileService.UnzipArchive`.

## Validation Risks

- A test that only checks non-null tools would miss incorrect permission classification.
- A test that writes directly to disk would bypass the workspace service boundary.
- The existing `Microsoft.OpenApi` NU1903 warning may remain during builds and should not be confused with this change.

## Reopen Triggers

- Any new filesystem operation bypasses `IWorkspaceFileService`.
- Any filesystem write tool is not approval-wrapped.
- Agents with read-only workspace access receive mutation/archive extraction tools.
- New tool names are absent from `ToolContractCatalog`, `ToolCapabilityRegistry`, or capability templates.
