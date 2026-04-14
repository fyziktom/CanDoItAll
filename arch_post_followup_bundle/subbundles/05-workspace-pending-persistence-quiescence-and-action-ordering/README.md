# Workspace pending-persistence quiescence and action ordering

## Status

- Prepared

## Objective

- Ensure that pending debounced canvas persistence cannot race publish, delete, export, process switching, or disposal, and make the workspace action order deterministic around local unsaved definition state.

## Covered Inputs

- See `02-open-findings.md`, `requirements/01-normalized-requirements.md`, and `traceability/01-finding-to-subbundle-map.md` for the owning findings and requirements.

## Prerequisites

- Follow the dependencies recorded in `codex/TASKS.json` and `plan/01-phase-plan.md`.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.DefinitionCrud.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.Persistence.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs

## Dependency Impact

- Downstream work remains blocked until this subbundle's progression gate is satisfied from fresh proof.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Audit the listed source references against the current live repository state.
2. Implement only the smallest correct change set for this subbundle.
3. Add or update the narrowest test surface that proves the stated invariant.
4. Run the required proof commands and capture fresh artifacts.
5. Update `reviews/01-execution-report.md` or the live execution report and the gate memo log before allowing downstream work to continue.

## Scope Exceptions

- Do not widen this subbundle beyond the stated objective. If the work uncovers a later-phase defect, record it and stop at the correct boundary.

## Do Not Do

- Do not continue into downstream numbered phases just because nearby files are already open.
- Do not mark this subbundle complete until the progression gate can be answered explicitly from real proof.
- If any workspace action still bypasses pending-persistence quiescence, fail immediately and open the workspace-quiescence corrective playbook.

## Acceptance Checklist

- Satisfy the deliverables and review questions preserved below.

## Proof Required

- Run the validation commands preserved below and record the resulting artifacts in the live execution report.

## Browser Validation Logging

- Only required if this subbundle changes visible `/processes` UI behavior beyond what component proof already covers.

## Progression Gate

- This phase is complete only when its acceptance checklist and proof artifacts are satisfied strongly enough for the next dependency to proceed without borrowed trust.

## Suggested Agent Prompt

```text
Implement only subbundle 05-workspace-pending-persistence-quiescence-and-action-ordering. Ensure that pending debounced canvas persistence cannot race publish, delete, export, process switching, or disposal, and make the workspace action order deterministic around local unsaved definition state. Respect the prerequisites, stop rules, and proof contract, update the live execution report from fresh evidence, and do not continue downstream until the progression gate is explicitly satisfied.
```

## Preserved Bundle Notes

### Purpose
Ensure that pending debounced canvas persistence cannot race publish, delete, export, process switching, or disposal, and make the workspace action order deterministic around local unsaved definition state.

### Required deliverables
- A single quiescence helper or equivalent orchestration rule used by Save, Publish, Delete, Export, selection changes, and disposal where needed.
- A guarantee that Publish cannot run against stale local authoring state when a canvas save is pending or in flight.
- A guarantee that Delete cannot be followed by an in-flight autosave that recreates the deleted definition under a new identity.
- A guarantee that Export reflects the current editor state or explicitly forces persistence first.
- Component and/or integration tests that cover pending autosave + publish, pending autosave + delete, and pending autosave + export.

### Repository touchpoints
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.DefinitionCrud.cs`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.Persistence.cs`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.cs`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs`
- `tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`

### Validation commands
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests" -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests" -v:minimal`

### Review questions
1. Do publish, delete, and export now quiesce pending definition persistence before they continue?
2. Can a pending or in-flight autosave still recreate a definition after delete or publish stale data ahead of local changes?
3. Are the new workspace tests strong enough to catch the ordering bugs again?

### Corrective trigger
If any workspace action still bypasses pending-persistence quiescence, fail immediately and open the workspace-quiescence corrective playbook.

### Corrective template
- `subbundles/_corrective-workspace-quiescence-reset`

### Detailed execution notes
- The main remaining cross-thread/race problem is here. I did not find a stronger backend threading bug than this UI-side action-order issue.
- Consider whether disposal needs an async drain/wait boundary rather than only a best-effort cancellation.

