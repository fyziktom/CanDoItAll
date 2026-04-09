# PRM-F24 — Live process execution canvas overlays and baton visibility

## Objective

Project live execution back onto the authored process canvas so the same diagram becomes the primary supervision surface for active runs without becoming canonical state.

## Priority and wave

- Priority: **High**
- Planned wave: **Wave 3**
- Depends on: **PRM-F07, PRM-F08, PRM-F09, PRM-F22**

## Why this feature exists

The user wants to see exactly what is happening in the process as a canvas or flow diagram. That visibility should exist on the same modeled diagram, but with strict projection boundaries.

## In scope

- Runtime overlay service for active process runs
- Canvas badges and cues for active, waiting, blocked, and completed steps
- Baton visibility and last handoff cues
- Timeline-to-canvas navigation without duplicate state ownership

## Non-goals

- Do not make live canvas overlays the canonical runtime state store.
- Do not block runtime supervision on advanced BPMN-style chrome.
- Do not fork a second visualization stack for live execution.

## Primary repo touchpoints

- `src/CanDoItAll.Modules.Processes/CanvasAdapters/ProcessCanvasGraphAdapter.cs`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeOverlayService.cs (new)`
- `src/CanDoItAll.Modules.Processes/Pages/ProcessDesignerPage.razor`
- `src/CanDoItAll.Modules.Processes/Pages/ProcessRunPage.razor (new)`
- `src/CanDoItAll.Components.CanvasLib/Canvas/Workbench/*`
- `tests/CanDoItAll.Tests.Component/ProcessRuntimeOverlayTests.cs (new)`
- `tests/CanDoItAll.Tests.Playwright/ProcessRunCanvasOverlayFlowTests.cs (new)`
