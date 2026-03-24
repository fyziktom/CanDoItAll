# Bundle Completion Checklist

Updated from the audited 62-component matrix on 2026-03-24.

## Source of truth

- Full component-by-component status:
  `C:\repositories\CanDoItAll\implementation-phase2\COMPONENT_MATRIX.md`
- Detailed runtime analysis:
  `C:\repositories\CanDoItAll\implementation-phase2\DOTNETWATCH_ANALYSIS.md`
- Bundle component specs:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\components`
- Bundle integration order:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\integration\IMPLEMENTATION_ORDER.md`

## Current audited counts

- `[x]` `62` components are `validated`
- `[x]` `0` components are `implemented`
- `[x]` `0` components are still only `inline`
- `[x]` `0` components are still truly `missing`

## dotnetwatch and validation runtime

- `[x]` Backend shadow-build confirmed under `.artifacts/mcp-server-shadow/builds`
- `[x]` Managed app session artifacts are isolated under
  `.mcp-state\artifacts\app-projects` and `.mcp-state\artifacts\app-sessions`
- `[x]` Managed app is configured as live-source `WatchRun` from
  `src/CanDoItAll.Web/CanDoItAll.Web.csproj`
- `[x]` The MCP runtime contract currently exposes only `WatchRun` and
  `RunOnce`; there is no published-app managed mode yet
- `[x]` Direct Codex-to-MCP calls such as `candoitall_workspace_info` still
  fail with generic invocation errors in this session
- `[x]` Manager-backed watch session is usable for browser validation when the
  direct bridge is broken
- `[x]` Manager-driven force rebuilds pick up current Razor and JS changes
- `[x]` Historical watch logs still show `dotnet watch` overload events during
  large same-solution edit waves
- `[x]` `DOTNET_USE_POLLING_FILE_WATCHER=1` is configured in the managed app
  environment
- `[x]` Release publish succeeded to
  `.artifacts\bundle-validation\webapp`
- `[x]` The old published host had to be stopped before republish because it
  locked the target DLLs
- `[x]` Release output is currently a manual validation target, not the native
  managed-app mode used by `dotnetwatch`
- `[x]` The broken direct `candoitall_*` tool bridge is documented in
  `DOTNETWATCH_ANALYSIS.md`

## Integration seams exercised during closeout

- `[x]` Shared graph runtime migration:
  `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor`,
  `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js`,
  `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css`
- `[x]` Calendar runtime migration:
  `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor`,
  `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js`
- `[x]` Project Structure integration:
  `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `[x]` Prompt Factory integration:
  `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor`
- `[x]` App-level script/module registration:
  `src/CanDoItAll.Web/Components/App.razor`

## Final closeout items

- `[x]` Wave 2 graph primitive validation gallery added to Prompt Factory
- `[x]` Project Structure adapter preview cards added and screenshot-validated
- `[x]` Prompt Factory undo/redo adapter preview card completed and
  screenshot-validated
- `[x]` Calendar boundary preview cards extracted, tested, and
  screenshot-validated
- `[x]` `COMPONENT_MATRIX.md` updated to record final evidence
- `[x]` This checklist updated to the fully closed state

## Verification completed

- `[x]` Focused component tests passed for the new page, adapter, and calendar
  boundaries
- `[x]` Manager-driven watch rebuild reached `Healthy / Ready`
- `[x]` Playwright screenshots now exist for all remaining visible bundle
  boundaries
