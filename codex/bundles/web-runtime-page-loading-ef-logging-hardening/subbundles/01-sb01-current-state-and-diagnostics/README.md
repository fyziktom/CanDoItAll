# SB01-current-state-and-diagnostics

## Status

- `Completed`

## Objective

Confirm the concrete eager-load and mutation-reload sources behind the reported regressions before implementation.

## Success Criteria

- The affected files and methods are identified.
- Each user concern maps to a specific implementation path.
- Downstream subbundles can edit with clear boundaries.

## Covered Inputs

- `REQ-PROC-001`
- `REQ-PROJ-001`
- `REQ-WF-001`
- `REQ-EF-001`

## Prerequisites

- none

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Loading.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs`
- `repo://src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `repo://src/CanDoItAll.Infrastructure/Configuration/AppOptions.cs`
- `repo://src/CanDoItAll.Web/Program.cs`

## Deliverables

- Current-state notes in `analysis/01-current-state.md`.
- Requirement and traceability mapping in `requirements/` and `traceability/`.

## Dependency Impact

- All implementation subbundles depend on this phase because the proof must target exact eager calls and reload behavior rather than speculative caching.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Inspect the named files and confirm the slow paths.
2. Update analysis and requirement docs.
3. Run prepared-bundle validation before code edits.

## Scope Exceptions

- No code changes are made in this phase.

## Do Not Do

- Do not introduce new telemetry or broad profiling framework.
- Do not change behavior before the bundle is prepared.

## Acceptance Checklist

- Current-state notes mention Processes, Project Structure, Workflows, and EF logging.
- Traceability maps every raw user concern.
- Prepared validation passes.

## Proof Required

- `python scripts/validate_bundle.py --stage prepared`

## Browser Validation Logging

- N/A

## Progression Gate

- Prepared bundle validation must pass before implementation starts.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
