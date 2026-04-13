# Differential definition-graph persistence

## Status

- `Completed`
- `2026-04-13`: `ProcessesService.SaveDefinitionChildrenAsync` now performs tracked differential persistence for roles, skills, steps, branch outcomes, dependencies, role assignments, artifact expectations, and artifact inputs; the normal save path no longer deletes and recreates the whole child graph, unchanged logical children keep stable IDs, and the targeted build/integration/MCP proof passed.

## Objective

- Replace the current delete-and-recreate definition save behavior with differential graph persistence so stable logical children retain identity and saves become less destructive.

## Covered Inputs

- `U003` Stabilization and maintainability concerns.
- `BRQ-008` Differential graph persistence.
- `F003` Destructive graph persistence.

## Prerequisites

- `05-transaction-concurrency-and-conflict-hardening` passed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Persistence.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionEnums.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionEntities.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionEntityConfigurations.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionEditorModels.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\ProcessesToolsTests.cs

## Deliverables

- A differential save engine or equivalent service for process-definition children.
- Stable child IDs across no-op and targeted saves.
- Removal of the delete-and-recreate graph rewrite path from the normal save flow.
- Tests proving stable identity and rollback safety.

## Dependency Impact

- Publication cloning, runtime references, and future auditability depend on child identity no longer churning unnecessarily.
- Gate B will explicitly reject weak differential-persistence proof.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Load the persisted aggregate graph in a form that allows stable child matching by ID.
2. Implement add/update/delete logic surgically for roles, steps, dependencies, branch outcomes, assignments, and related child collections.
3. Remove intermediate graph-wide delete-and-recreate behavior from the normal save path.
4. Ensure the full save participates in the transaction/concurrency rules added in subbundle 05.
5. Add tests for no-op save stability, targeted updates, targeted deletes, and rollback integrity.

## Scope Exceptions

- This phase does not redesign the public save API.
- This phase does not yet refactor publish or clone logic beyond what is needed to remain compatible with the new save behavior.

## Do Not Do

- Do not keep the destructive rewrite path as a hidden fallback for normal saves.
- Do not reassign IDs for unchanged logical children.
- Do not add more intermediate `SaveChangesAsync` checkpoints inside the graph mutation flow.

## Acceptance Checklist

- No-op save preserves child IDs.
- Editing one step or role does not recreate unrelated children.
- Artifact, assignment, dependency, and branch links remain intact across targeted updates.
- Rollback tests show no partial child graph remains after failure.

## Proof Required

- Integration tests proving stable child identity.
- Regression proof for save, publish-clone compatibility, and MCP save behavior.
- Execution-report notes describing how the diff engine matches children.

## Browser Validation Logging

- N/A.

## Progression Gate

- Differential persistence is the normal save path, unchanged logical children keep stable identity, and rollback behavior is proven strong enough for later publication/runtime work.

## Suggested Agent Prompt

```text
Implement only subbundle 06. Replace the destructive definition save path with differential graph persistence that preserves stable child IDs and participates in the transaction/concurrency rules from subbundle 05. Add strong stability tests and stop before publish/runtime decomposition.
```
