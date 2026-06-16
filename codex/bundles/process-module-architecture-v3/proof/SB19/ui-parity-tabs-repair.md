# SB19 UI Parity Tabs Repair

## Trigger

The follow-up review found the process workspace was still too vertically stacked. The requested repair was to analyze the original `maf-processes-refactor` process UI, restore the original process detail tab system, keep the shared canvas process editor UX, use a treeview for definitions, and ensure Live Processes appears as a process-module menu item.

## Original UI References Used

- `maf-processes-refactor:src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor`
- `maf-processes-refactor:src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceStepsTab.razor`
- `maf-processes-refactor:src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsTab.razor`
- `maf-processes-refactor:tests/CanDoItAll.Tests.Playwright/ProcessWorkspace*.cs`
- `maf-processes-refactor:tests/CanDoItAll.Tests.Components/ProcessWorkspace*.cs`

The original branch could not be launched for browser screenshots because it does not compile in this workspace. The failing branch build reports missing `CanDoItAll.Processes.Drivers.Abstractions.Evidence` and missing `ProcessDriverEvidenceReference` types in `src/CanDoItAll.Processes.Drivers.Abstractions`. Source and test analysis was still usable and was used as the parity baseline.

## Repair Summary

- Restored original-style process detail tabs: `Definition`, `Roles`, `Steps`, `Runs`, `Graphs`, `Analytics`, `Exchange`, and `Manager chat`.
- Moved definition roles, steps/canvas, template exchange, runtime runs, graph metrics, analytics, and manager context out of a vertical stack into dedicated tabs.
- Preserved the shared process canvas editor in the `Steps` tab, including CanvasLib workbench and floating toolbox/selection/editor windows over the canvas.
- Replaced the definition list pane with the shared `TreeView`, including `All definitions`, global/project scope nodes, and definition child rows.
- Restored nested run tabs for `Launch`, `Activity`, `Control`, `Execution`, `Graphs`, `Coordination`, and `Evidence`.
- Added process-level graph and manager-chat tabs while retaining projection-first boundaries for cost/token/time and agent context data.
- Fixed contributed navigation ordering so `/processes/live` is inserted immediately after `/processes` instead of being orphaned into overflow.

## Validation

```text
dotnetwatch build src/CanDoItAll.Web/CanDoItAll.Web.csproj
Result: passed, 0 warnings, 0 errors.
```

```text
dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceShellTests|FullyQualifiedName~ShellNavigationContributionTests|FullyQualifiedName~AppShellTests" --logger "console;verbosity=minimal"
Result: passed, 33/33.
```

```text
dotnetwatch build tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj
Result: passed, 0 warnings, 0 errors.
```

```text
dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --no-build --filter FullyQualifiedName~ProcessShellSmokeTests.Process_shell_routes_render_global_and_project_scoped_workspaces --logger "console;verbosity=minimal"
Result: passed, 1/1.
```

```text
CanDoItAll CodeAnalytics MCP snapshot:
Snapshot: snap-20260616120824-b9b86e2f
Scope: CanDoItAll.Modules.Processes, CanDoItAll.Web, CanDoItAll.AppComponents, CanDoItAll.SharedKernel, CanDoItAll.Tests.Components, CanDoItAll.Tests.Playwright.
Result: no blocking errors.
```

## Browser Artifacts

- bundle://proof/SB19/ui-parity-tabs-repair/browser/before-current-stacked-processes.png
- bundle://proof/SB19/ui-parity-tabs-repair/browser/after-tabs-tree-definition.png
- bundle://proof/SB19/ui-parity-tabs-repair/browser/after-steps-canvas-floating-windows.png
- bundle://proof/SB19/ui-parity-tabs-repair/browser/after-graphs-cost-token-time.png
- bundle://proof/SB19/ui-parity-tabs-repair/browser/after-manager-chat.png
- bundle://proof/SB19/ui-parity-tabs-repair/browser/after-live-processes-menu-order.png
- bundle://proof/SB19/ui-parity-tabs-repair/browser/after-live-processes-subpage.png

## Result

Repair passed. The process workspace now follows the original tree/list plus tabbed detail model much more closely, the process canvas editor remains the shared floating-window canvas system, and Live Processes is visible directly under Processes in the main navigation and opens `/processes/live`.
