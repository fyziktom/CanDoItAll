# PRM-F09 — Canvas modeler and interactive diagrams

## Objective

Deliver a dedicated process designer on top of CanvasLib with reusable node rendering, quick-create affordances, saved layout, template-aware role editing, and a path to labeled transitions and swimlane-like grouping.

## Priority and wave

- Priority: **Critical**
- Planned wave: **Wave 1**
- Depends on: **PRM-F01, PRM-F02, PRM-F03**

## Why this feature exists

This feature is part of the first process-management bundle because the user explicitly wants process definitions, actor responsibility, handoffs, and interactive modeling to land **before** the intelligence lake and before deep runtime coupling to the AgentFramework overlay.

## In scope

- Users can create and edit process nodes and transitions from an interactive canvas surface.
- Diagram layout persists independently from the canonical graph semantics.
- Phase grouping and actor grouping can be represented without forcing Workbench to be the source of truth.
- Wave 1 designer delivery is not blocked by later handoff-label chrome; handoff visuals can deepen in Wave 2.
- The design leaves room for labeled transitions and swimlane extensions where CanvasLib needs them.

## Non-goals

- Do not fork a separate canvas technology stack.
- Do not make diagram layout canonical.

## Primary repo touchpoints

- `src/CanDoItAll.Modules.Processes/CanvasAdapters/ProcessCanvasGraphAdapter.cs (new)`
- `src/CanDoItAll.Modules.Processes/Pages/ProcessDesignerPage.razor (new)`
- `src/CanDoItAll.Modules.Processes/ProcessActorTemplateBridge.cs (new or reused)`
- `src/CanDoItAll.Components.CanvasLib/Canvas/Workbench/CanvasWorkbenchSurface.cs`
- `src/CanDoItAll.Components.CanvasLib/Canvas/Workbench/CanvasWorkbenchNode.cs`
- `src/CanDoItAll.Components.CanvasLib/Canvas/Workbench/CanvasWorkbenchEvents.cs`
- `src/CanDoItAll.Modules.Factory/CanvasAdapters/* (reference pattern)`
- `tests/CanDoItAll.Tests.Components/ProcessDesignerPageTests.cs (new)`
- `tests/CanDoItAll.Tests.Playwright/ProcessModelingFlowTests.cs (new)`
