# PRM-F10 — Project, Workbench, and shell projections

## Objective

Expose process navigation in project UX and optional Workbench projection surfaces while keeping canonical process data outside Workbench.

## Priority and wave

- Priority: **High**
- Planned wave: **Wave 3**
- Depends on: **PRM-F01, PRM-F02, PRM-F07, PRM-F09**

## Why this feature exists

This feature is part of the first process-management bundle because the user explicitly wants process definitions, actor responsibility, handoffs, and interactive modeling to land **before** the intelligence lake and before deep runtime coupling to the AgentFramework overlay.

## In scope

- Project surfaces expose a clear entry point into processes.
- If Workbench projection is enabled, it shows references and summaries rather than acting as the canonical store.
- Process-related project object types and routes remain explicit and typed.
- Shared-project processes are navigated through project ownership rather than duplicated shadow copies.

## Non-goals

- Do not turn project UX into a second full process editor.
- Do not make Workbench the only route to process data.

## Primary repo touchpoints

- `src/CanDoItAll.Modules.Projects/Pages/Components/ProjectModalHost.razor`
- `src/CanDoItAll.Modules.Projects/Pages/Components/ProjectsBoard.razor`
- `src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs`
- `src/CanDoItAll.Modules.Workbench/* (projection-only integration)`
- `tests/CanDoItAll.Tests.Components/ProjectProcessesNavigationTests.cs (new)`
