# Detailed Phase 2 Checklist

Updated from the audited repo and bundle state on 2026-03-24.

This checklist tracks the bundle work that was still open after the first
phase-2 audit pass and is now closed. Each item includes the bundle reference,
target repo boundary, primary integration seam, and intended screenshot
surface. Final screenshot evidence lives in `COMPONENT_MATRIX.md`.

## Runtime and validation rules

- Use CanDoItAll `dotnetwatch` MCP manager, Playwright MCP, and screenshots for
  validation workflow control.
- Treat the direct `candoitall_*` tool bridge as broken until proven otherwise.
- The MCP server itself already runs from shadow builds under
  `.artifacts\mcp-server-shadow\builds`.
- The managed app runtime still supports only `WatchRun` and `RunOnce` from a
  project path; there is no published-app mode in the current server contract.
- Use `C:\repositories\CanDoItAll\.artifacts\bundle-validation\webapp` as the
  release-style publish target for bundle closeout.
- Use manager-backed `WatchRun` when live-source validation is required.
- Do not run large builds against the same solution while a watch-managed app
  session is rebuilding.

## Wave 2 shared graph boundaries

### `ChipBadgePrimitive`

- Bundle ref:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\components\ChipBadgePrimitive\README.md`
- Target boundary:
  `src/CanDoItAll.ComponentKit/Canvas/Graph/ChipBadgePrimitive.cs`
- Test target:
  `tests/CanDoItAll.Tests.Components/ChipBadgePrimitiveTests.cs`
- Integration seam:
  `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor`
- Validation surface:
  CanvasWorkbench settings preview on Prompt Factory

### `ConnectorPathPrimitive`

- Bundle ref:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\components\ConnectorPathPrimitive\README.md`
- Target boundary:
  `src/CanDoItAll.ComponentKit/Canvas/Graph/ConnectorPathPrimitive.cs`
- JS boundary:
  `src/CanDoItAll.ComponentKit/wwwroot/js/connector-path-primitive.js`
- Test target:
  `tests/CanDoItAll.Tests.Components/ConnectorPathPrimitiveTests.cs`
- Integration seam:
  `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor`
- Validation surface:
  CanvasWorkbench settings preview on Prompt Factory

### `ContainerPrimitive`

- Bundle ref:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\components\ContainerPrimitive\README.md`
- Target boundary:
  `src/CanDoItAll.ComponentKit/Canvas/Graph/ContainerPrimitive.cs`
- JS boundary:
  `src/CanDoItAll.ComponentKit/wwwroot/js/container-primitive.js`
- Test target:
  `tests/CanDoItAll.Tests.Components/ContainerPrimitiveTests.cs`
- Integration seam:
  `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor`
- Validation surface:
  CanvasWorkbench settings preview on Prompt Factory

### `ContextMenuHost`

- Bundle ref:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\components\ContextMenuHost\README.md`
- Target boundaries:
  `src/CanDoItAll.ComponentKit/Canvas/Graph/ContextMenuHost.cs`
  `src/CanDoItAll.ComponentKit/Components/ContextMenuHost.razor`
- JS boundary:
  `src/CanDoItAll.ComponentKit/wwwroot/js/context-menu-host.js`
- Test target:
  `tests/CanDoItAll.Tests.Components/ContextMenuHostTests.cs`
- Integration seam:
  `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor`
- Validation surface:
  CanvasWorkbench settings preview on Prompt Factory

### `CreateActionPalette`

- Bundle ref:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\components\CreateActionPalette\README.md`
- Target boundaries:
  `src/CanDoItAll.ComponentKit/Canvas/Graph/CreateActionPalette.cs`
  `src/CanDoItAll.ComponentKit/Components/CreateActionPalette.razor`
- JS boundary:
  `src/CanDoItAll.ComponentKit/wwwroot/js/create-action-palette.js`
- Test target:
  `tests/CanDoItAll.Tests.Components/CreateActionPaletteTests.cs`
- Integration seam:
  `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor`
- Validation surface:
  CanvasWorkbench settings preview on Prompt Factory

### `FloatingInspectorHost`

- Bundle ref:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\components\FloatingInspectorHost\README.md`
- Target boundaries:
  `src/CanDoItAll.ComponentKit/Canvas/Graph/FloatingInspectorHost.cs`
  `src/CanDoItAll.ComponentKit/Components/FloatingInspectorHost.razor`
- JS boundary:
  `src/CanDoItAll.ComponentKit/wwwroot/js/floating-inspector-host.js`
- Test target:
  `tests/CanDoItAll.Tests.Components/FloatingInspectorHostTests.cs`
- Integration seams:
  `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor`
  `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor`
- Validation surface:
  CanvasWorkbench settings preview on Prompt Factory

### `GroupFrameOverlay`

- Bundle ref:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\components\GroupFrameOverlay\README.md`
- Target boundaries:
  `src/CanDoItAll.ComponentKit/Canvas/Graph/GroupFrameOverlay.cs`
  `src/CanDoItAll.ComponentKit/Components/GroupFrameOverlay.razor`
- JS boundary:
  `src/CanDoItAll.ComponentKit/wwwroot/js/group-frame-overlay.js`
- Test target:
  `tests/CanDoItAll.Tests.Components/GroupFrameOverlayTests.cs`
- Integration seam:
  `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor`
- Validation surface:
  CanvasWorkbench settings preview on Prompt Factory

### `IconGlyphPrimitive`

- Bundle ref:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\components\IconGlyphPrimitive\README.md`
- Target boundary:
  `src/CanDoItAll.ComponentKit/Canvas/Graph/IconGlyphPrimitive.cs`
- Test target:
  `tests/CanDoItAll.Tests.Components/IconGlyphPrimitiveTests.cs`
- Integration seam:
  `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor`
- Validation surface:
  CanvasWorkbench settings preview on Prompt Factory

### `ImagePrimitive`

- Bundle ref:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\components\ImagePrimitive\README.md`
- Target boundary:
  `src/CanDoItAll.ComponentKit/Canvas/Graph/ImagePrimitive.cs`
- JS boundary:
  `src/CanDoItAll.ComponentKit/wwwroot/js/image-primitive.js`
- Test target:
  `tests/CanDoItAll.Tests.Components/ImagePrimitiveTests.cs`
- Integration seam:
  `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor`
- Validation surface:
  CanvasWorkbench settings preview on Prompt Factory

### `InlineEditorComposer`

- Bundle ref:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\components\InlineEditorComposer\README.md`
- Target boundaries:
  `src/CanDoItAll.ComponentKit/Canvas/Graph/InlineEditorComposer.cs`
  `src/CanDoItAll.ComponentKit/Components/InlineEditorComposer.razor`
- JS boundary:
  `src/CanDoItAll.ComponentKit/wwwroot/js/inline-editor-composer.js`
- Test target:
  `tests/CanDoItAll.Tests.Components/InlineEditorComposerTests.cs`
- Integration seam:
  `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor`
- Validation surface:
  CanvasWorkbench settings preview on Prompt Factory

### `NodeCardComposer`

- Bundle ref:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\components\NodeCardComposer\README.md`
- Target boundaries:
  `src/CanDoItAll.ComponentKit/Canvas/Graph/NodeCardComposer.cs`
  `src/CanDoItAll.ComponentKit/Components/NodeCardComposer.razor`
- JS boundary:
  `src/CanDoItAll.ComponentKit/wwwroot/js/node-card-composer.js`
- Test target:
  `tests/CanDoItAll.Tests.Components/NodeCardComposerTests.cs`
- Integration seam:
  `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor`
- Validation surface:
  CanvasWorkbench settings preview on Prompt Factory

### `TextBlockPrimitive`

- Bundle ref:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\components\TextBlockPrimitive\README.md`
- Target boundary:
  `src/CanDoItAll.ComponentKit/Canvas/Graph/TextBlockPrimitive.cs`
- JS boundary:
  `src/CanDoItAll.ComponentKit/wwwroot/js/text-block-primitive.js`
- Test target:
  `tests/CanDoItAll.Tests.Components/TextBlockPrimitiveTests.cs`
- Integration seam:
  `src/CanDoItAll.ComponentKit/Components/CanvasWorkbench.razor`
- Validation surface:
  CanvasWorkbench settings preview on Prompt Factory

## Wave 3 project structure adapters

### `ProjectStructureActionCatalogAdapter`

- Bundle ref:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\components\ProjectStructureActionCatalogAdapter\README.md`
- Target boundary:
  `src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureActionCatalogAdapter.cs`
- Test target:
  `tests/CanDoItAll.Tests.Components/ProjectStructureActionCatalogAdapterTests.cs`
- Integration seam:
  `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- Validation surface:
  Project Structure page or targeted validation card with screenshot

### `ProjectStructurePlacementPolicy`

- Bundle ref:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\components\ProjectStructurePlacementPolicy\README.md`
- Target boundary:
  `src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructurePlacementPolicy.cs`
- Test target:
  `tests/CanDoItAll.Tests.Components/ProjectStructurePlacementPolicyTests.cs`
- Integration seam:
  `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- Validation surface:
  Project Structure page or targeted validation card with screenshot

## Wave 4 prompt factory adapter

### `PromptFactoryUndoRedoAdapter`

- Bundle ref:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\components\PromptFactoryUndoRedoAdapter\README.md`
- Target boundaries:
  `src/CanDoItAll.Modules.Factory/CanvasAdapters/PromptFactoryUndoRedoAdapter.cs`
  `src/CanDoItAll.Modules.Factory/wwwroot/js/prompt-factory-undo-redo-adapter.js`
- Test target:
  `tests/CanDoItAll.Tests.Components/PromptFactoryUndoRedoAdapterTests.cs`
- Integration seams:
  `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor`
  `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs`
- Validation surface:
  Prompt Factory page with history/selection preview screenshot

## Wave 5 calendar boundaries

### `CalendarCrudBridge`

- Bundle ref:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\components\CalendarCrudBridge\README.md`
- Target boundary:
  `src/CanDoItAll.ComponentKit/Canvas/Calendar/CalendarCrudBridge.cs`
- JS boundary:
  `src/CanDoItAll.ComponentKit/wwwroot/js/calendar-crud-bridge.js`
- Test target:
  `tests/CanDoItAll.Tests.Components/CalendarCrudBridgeTests.cs`
- Integration seams:
  `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor`
  `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor`
- Validation surface:
  Project Calendar page

### `CalendarEventEditorModal`

- Bundle ref:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\components\CalendarEventEditorModal\README.md`
- Target boundaries:
  `src/CanDoItAll.ComponentKit/Canvas/Calendar/CalendarEventEditorModal.cs`
  `src/CanDoItAll.ComponentKit/Components/CalendarEventEditorModal.razor`
- JS boundary:
  `src/CanDoItAll.ComponentKit/wwwroot/js/calendar-event-editor-modal.js`
- Test target:
  `tests/CanDoItAll.Tests.Components/CalendarEventEditorModalTests.cs`
- Integration seams:
  `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor`
  `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor`
- Validation surface:
  Project Calendar page

### `CalendarExportMenu`

- Bundle ref:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\components\CalendarExportMenu\README.md`
- Target boundaries:
  `src/CanDoItAll.ComponentKit/Canvas/Calendar/CalendarExportMenu.cs`
  `src/CanDoItAll.ComponentKit/Components/CalendarExportMenu.razor`
- JS boundary:
  `src/CanDoItAll.ComponentKit/wwwroot/js/calendar-export-menu.js`
- Test target:
  `tests/CanDoItAll.Tests.Components/CalendarExportMenuTests.cs`
- Integration seams:
  `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor`
  `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor`
- Validation surface:
  Project Calendar page

### `CalendarMiniMonthNavigator`

- Bundle ref:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\components\CalendarMiniMonthNavigator\README.md`
- Target boundaries:
  `src/CanDoItAll.ComponentKit/Canvas/Calendar/CalendarMiniMonthNavigator.cs`
  `src/CanDoItAll.ComponentKit/Components/CalendarMiniMonthNavigator.razor`
- JS boundary:
  `src/CanDoItAll.ComponentKit/wwwroot/js/calendar-mini-month-navigator.js`
- Test target:
  `tests/CanDoItAll.Tests.Components/CalendarMiniMonthNavigatorTests.cs`
- Integration seams:
  `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor`
  `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor`
- Validation surface:
  Project Calendar page

### `CalendarSelectionPanel`

- Bundle ref:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\components\CalendarSelectionPanel\README.md`
- Target boundaries:
  `src/CanDoItAll.ComponentKit/Canvas/Calendar/CalendarSelectionPanel.cs`
  `src/CanDoItAll.ComponentKit/Components/CalendarSelectionPanel.razor`
- JS boundary:
  `src/CanDoItAll.ComponentKit/wwwroot/js/calendar-selection-panel.js`
- Test target:
  `tests/CanDoItAll.Tests.Components/CalendarSelectionPanelTests.cs`
- Integration seams:
  `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor`
  `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor`
- Validation surface:
  Project Calendar page

### `CalendarTimeGridRenderer`

- Bundle ref:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\components\CalendarTimeGridRenderer\README.md`
- Target boundary:
  `src/CanDoItAll.ComponentKit/Canvas/Calendar/CalendarTimeGridRenderer.cs`
- JS boundary:
  `src/CanDoItAll.ComponentKit/wwwroot/js/calendar-time-grid-renderer.js`
- Test target:
  `tests/CanDoItAll.Tests.Components/CalendarTimeGridRendererTests.cs`
- Integration seams:
  `src/CanDoItAll.ComponentKit/Components/CanvasCalendar.razor`
  `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor`
- Validation surface:
  Project Calendar page

### `ProjectCalendarStateParser`

- Bundle ref:
  `C:\repositories\CanDoItAll\CanDoItAll_CanvasFramework_CodexBundle\components\ProjectCalendarStateParser\README.md`
- Target boundary:
  `src/CanDoItAll.Modules.Workbench/Calendar/ProjectCalendarStateParser.cs`
- Test target:
  `tests/CanDoItAll.Tests.Components/ProjectCalendarStateParserTests.cs`
- Integration seam:
  `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor`
- Validation surface:
  Project Calendar page plus parser preview screenshot
