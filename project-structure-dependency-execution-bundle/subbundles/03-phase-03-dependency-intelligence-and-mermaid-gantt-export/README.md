# Phase 03 dependency intelligence and Mermaid gantt export

## Status

- `Completed`

## Objective

- Build a reusable dependency-analysis surface and make Mermaid Gantt export depend on the same graph and duration semantics.

## Covered Inputs

- `N008`
- `N009`
- `N010`
- `RQ-009`
- `RQ-010`
- `RQ-011`
- `RQ-012`
- `NFR-001`

## Prerequisites

- `subbundles/01-phase-01-models-persistence-and-mcp-dependency-surfaces`

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureChecklistService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureSummaryModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureAgentContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureAgentService.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs

## Deliverables

- Reusable dependency-analysis service or driver that can answer prerequisite and readiness questions from persisted graph data.
- Mermaid Gantt generation that respects dependency ordering and explicit or default duration behavior.
- Tests that prove duration fallback to one hour when no explicit duration exists.

## Dependency Impact

- Phase 04 validation data and future MCP consumers depend on this phase to interpret the graph correctly.
- Weak proof here would leave the user-visible dependency feature without the requested downstream scheduling value.

## Validation Depth

- `Foundation plus deterministic export proof`

## Implementation Steps

1. Introduce a dependency-analysis model that computes prerequisites, dependents, and readiness from the stored graph.
2. Expose the analysis where needed for MCP or service consumers.
3. Update Mermaid Gantt export to use explicit duration seconds when present and a one-hour fallback when missing.
4. Add deterministic tests for readiness answers and Mermaid export ordering and default duration behavior.

## Scope Exceptions

- Final browser proof and fresh-SQLite seed creation belong to Phase 04.

## Do Not Do

- Do not build a second independent dependency graph implementation just for Mermaid export.
- Do not hard-code duration fallback in one consumer while leaving other consumers inconsistent.

## Acceptance Checklist

- Dependency analysis can identify when a node is blocked versus ready.
- Dependents and prerequisites are discoverable in a reusable read model.
- Mermaid export uses explicit duration seconds or one-hour fallback in a deterministic way.
- Tests prove the graph semantics and export output are aligned.

## Proof Required

- Targeted integration or unit tests covering dependency analysis and Mermaid export.
- Targeted build if public contracts or summaries change.
- Manual inspection of Mermaid output in test assertions or snapshots.

## Browser Validation Logging

- N/A for direct browser closure in this phase.
- Reopen this phase if Phase 04 seeded-structure proof shows readiness or Gantt behavior disagreeing with the stored dependency graph.

## Progression Gate

- Do not allow final closure until readiness answers and Mermaid export are covered by deterministic tests using the persisted dependency graph.

## Suggested Agent Prompt

```text
Implement Phase 03 only.

Build a reusable dependency-analysis surface and wire Mermaid Gantt export to it.
Honor explicit duration seconds and default missing durations to one hour.
Do not spend this phase on toolbar interaction or final browser proof.
```
