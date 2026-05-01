# file-and-ipfs-open-actions

## Status

- `Completed`

## Objective

Expose the correct file action for file-related project-structure nodes: File Explorer for trusted local/managed drive files and browser new-tab open for IPFS-backed nodes.

## Covered Inputs

- `N003`
- `N004`
- `REQ-FILE-001`
- `REQ-FILE-002`
- `REQ-FILE-003`
- `REQ-FILE-004`

## Prerequisites

- Subbundle 01 completed or honestly blocked.
- Quick-action modal and context-menu action patterns from subbundle 01 remain trusted.

## Exact Source References

- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.NodeQuickActions.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.NodeEditing.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureLocalFileOpener.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureActionCatalogAdapter.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Infrastructure/Storage/Models/StorageModels.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Web/Infrastructure/ManagedFilesEndpointRoutes.cs`
- `C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/ProjectStructureLocalFileOpenerManagedFilesTests.cs`
- `C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Components/ProjectStructureActionCatalogAdapterTests.cs`
- `C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs`

## Deliverables

- Local/managed file nodes offer "Open in File Explorer" where node actions are shown.
- IPFS-backed file nodes offer "Open in New Tab" when a route is available.
- IPFS-only nodes do not incorrectly offer File Explorer.
- Double-click file behavior offers the requested action path instead of always bypassing to inline preview.
- Tests cover local file and IPFS action capability detection.

## Dependency Impact

- Subbundle 03 depends on these final local/IPFS capability semantics for MCP/internal-agent metadata.

## Validation Depth

- Critical UI, browser-action, and host-action foundation.

## Implementation Steps

1. Add or reuse a central helper that detects local-file and IPFS/new-tab capabilities from node storage and route metadata.
2. Use `CanShowLocalOpen(node)` and `IProjectStructureLocalFileOpener` for File Explorer eligibility and execution.
3. Detect IPFS references from `StorageObjectReference.ProviderKind == StorageProviderKind.Ipfs` and routable IPFS/open routes.
4. Add file actions to the quick-action modal and context menu using existing action components.
5. Ensure action dispatch routes File Explorer through `OpenAttachmentLocallyAsync` and new-tab open through `OpenArtifactInNewTabAsync`.
6. Add tests for local-only, IPFS-only, and unrelated nodes.

## Scope Exceptions

- No new IPFS gateway configuration or file retrieval behavior.
- No direct File Explorer launch for unsafe or blocked local files.

## Do Not Do

- Do not open local paths directly from UI code.
- Do not show File Explorer for IPFS-only nodes.
- Do not remove inline preview; offer open actions alongside preview where appropriate.

## Acceptance Checklist

- Trusted local file nodes show File Explorer action.
- IPFS-backed nodes show new-tab action.
- IPFS-only nodes do not show File Explorer action.
- New-tab action uses the existing browser open path.
- Tests cover action visibility.

## Completion Evidence

- Double-click quick actions now prefer `Open in File Explorer` for trusted local file nodes and `Open in New Tab` for IPFS-backed routable nodes.
- Canvas context actions now receive local-file and new-tab capability flags and expose `open-local` and `open-new-tab`.
- Canvas action dispatch routes local open through `OpenAttachmentLocallyAsync` and IPFS/new-tab open through `OpenArtifactInNewTabAsync`.
- Inspector actions include the IPFS/new-tab action when applicable.
- `ProjectStructureActionCatalogAdapterTests` covers local file and IPFS action visibility.

## Proof Required

- Targeted tests for local file opener and action catalog/page state.
- Browser proof for quick-action modal and context menu action visibility.
- Host validation note for File Explorer behavior.

## Browser Validation Logging

- Route: `/projects/{projectId}/structure`.
- Viewports: large desktop first; narrower pass only if affected.
- Actions/assertions: open a local file node modal/context menu and assert File Explorer; open an IPFS node modal/context menu and assert Open in New Tab.
- Screenshots: record modal and context-menu evidence paths in `reviews/01-execution-report.md`.
- Review questions: readability, clipping, lateral overflow, z-order, alignment, and no text overlap.

## Progression Gate

- Subbundle 03 may start only after local file and IPFS action semantics are stable and represented by tests or documented blockers.

## Suggested Agent Prompt

```text
Implement only subbundle 02. Add File Explorer and IPFS/new-tab action offers for file-related project-structure nodes using existing guarded services and browser open helpers. Update tests and execution-report proof.
```
