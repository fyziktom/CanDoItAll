# SB19 UI Parity Repair

## Trigger

User review found the first SB19 UI implementation reshaped the process module too far away from the original `maf-processes-refactor` UX. The repair target was to preserve the original process workspace layout patterns and use the shared process canvas editor system with floating canvas windows.

## Original UI References Used

- `maf-processes-refactor:src/CanDoItAll.Web/Components/Pages/ProcessesPage.razor`
- `maf-processes-refactor:src/CanDoItAll.Web/Components/Pages/LiveProcessesPage.razor`
- `maf-processes-refactor:src/CanDoItAll.Web/Components/Processes/ProcessWorkspace.razor`
- `maf-processes-refactor:src/CanDoItAll.Web/Components/Processes/ProcessCanvasToolboxWindow.razor`
- `src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor`

## Repair Summary

- Replaced the custom process SVG/card canvas with the shared `CanvasWorkbench`, `CanvasWorkbenchStage`, `CanvasFloatingWindow`, and `OverlayComponentToolbox` integration.
- Restored the process workspace toward the original dense `Toolbar` plus `ListDetailShell` model with a definition list pane and tabbed detail pane.
- Follow-up repair restored the original detail tab set: Definition, Roles, Steps, Runs, Graphs, Analytics, Exchange, and Manager chat.
- Follow-up repair restored the definition list as a shared TreeView and fixed Live Processes navigation ordering under Processes.
- Preserved the original canvas UX model: toolbox, selection, and editor live in floating canvas windows over the canvas surface.
- Added CanvasLib and OverlayLib references to the process module instead of rendering a bespoke canvas.
- Added a dedicated `LiveProcessesDashboard` component and kept `/processes/live` as an owned process-module page, with its own main-menu child item.
- Updated browser coverage to validate CanvasLib workbench/canvas presence and live dashboard routing without depending on private CanvasLib DOM node internals.

## Validation

```text
dotnet build src\CanDoItAll.Modules.Processes\CanDoItAll.Modules.Processes.csproj --no-restore
Result: passed, 0 warnings, 0 errors.
```

```text
dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter FullyQualifiedName~ProcessWorkspaceShellTests --logger "trx;LogFileName=process-ui-repair-components.trx" --logger "console;verbosity=minimal" --no-restore
Result: passed, 24/24.
```

```text
dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --filter FullyQualifiedName~ProcessShellSmokeTests --logger "trx;LogFileName=process-ui-repair-playwright.trx" --logger "console;verbosity=minimal" --no-restore
Result: passed, 1/1.
```

```text
CanDoItAll CodeAnalytics MCP snapshot:
Snapshot: snap-20260616120824-b9b86e2f
Scope: CanDoItAll.Modules.Processes, CanDoItAll.Web, CanDoItAll.AppComponents, CanDoItAll.SharedKernel, CanDoItAll.Tests.Components, CanDoItAll.Tests.Playwright.
Result: no blocking errors.
```

## Browser Artifacts

- bundle://proof/SB19/browser/processes-definition-canvas.png
- bundle://proof/SB19/browser/processes-live-dashboard.png
- bundle://proof/SB19/browser/processes-global-definition-catalog.png
- bundle://proof/SB19/browser/processes-project-shell.png
- bundle://proof/SB19/browser/browser-validation-summary.txt
- bundle://proof/SB19/ui-parity-tabs-repair/browser/after-tabs-tree-definition.png
- bundle://proof/SB19/ui-parity-tabs-repair/browser/after-steps-canvas-floating-windows.png
- bundle://proof/SB19/ui-parity-tabs-repair/browser/after-live-processes-menu-order.png

## Result

Repair passed. The process module now uses the shared CanvasLib/OverlayLib canvas system and retains the original tree/list, tabbed detail workspace, and live navigation shape much more closely while keeping the new projection/versioning contracts from the process architecture bundle.
