# Process graph DAG invariant hardening

## Status

- Prepared

## Objective

- Make the process graph legally acyclic, reject self-loops and dependency cycles at save/publish time, and remove the current runtime/canvas fallbacks that silently compensate for invalid cyclic graphs.

## Covered Inputs

- See `02-open-findings.md`, `requirements/01-normalized-requirements.md`, and `traceability/01-finding-to-subbundle-map.md` for the owning findings and requirements.

## Prerequisites

- Follow the dependencies recorded in `codex/TASKS.json` and `plan/01-phase-plan.md`.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Support.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasRecompositionService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasBranching.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessStepDependencyCollection.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessCanvasSurfaceFactoryTests.cs

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
- If any cyclic graph can still slip through or any fallback still silently compensates for it, fail the gate and open the graph-invariant corrective playbook.

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
Implement only subbundle 02-process-graph-dag-invariant-hardening. Make the process graph legally acyclic, reject self-loops and dependency cycles at save/publish time, and remove the current runtime/canvas fallbacks that silently compensate for invalid cyclic graphs. Respect the prerequisites, stop rules, and proof contract, update the live execution report from fresh evidence, and do not continue downstream until the progression gate is explicitly satisfied.
```

## Preserved Bundle Notes

### Purpose
Make the process graph legally acyclic, reject self-loops and dependency cycles at save/publish time, and remove the current runtime/canvas fallbacks that silently compensate for invalid cyclic graphs.

### Required deliverables
- A graph-validation path that rejects self-dependencies and multi-step dependency cycles before save/publish succeeds.
- Removal of the `StartRunAsync` fallback that seeds the first step when no roots exist.
- Removal or fail-fast replacement of the canvas topological-order fallback that appends unresolved nodes silently.
- Fresh tests proving invalid cyclic graphs are rejected and valid branching DAGs still run and render correctly.

### Repository touchpoints
- `src/CanDoItAll.Modules.Processes/ProcessesService.Support.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs`
- `src/CanDoItAll.Modules.Processes/ProcessCanvasRecompositionService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessCanvasBranching.cs`
- `src/CanDoItAll.Modules.Processes/ProcessStepDependencyCollection.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Components/ProcessCanvasSurfaceFactoryTests.cs`

### Validation commands
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests" -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessWorkspaceTests" -v:minimal`

### Review questions
1. Can a self-loop or multi-step dependency cycle still be saved or published?
2. Does runtime startup still contain any silent fallback when the graph has no legal roots?
3. Does the canvas now fail loudly or diagnose invalid graph order instead of silently appending unresolved nodes?

### Corrective trigger
If any cyclic graph can still slip through or any fallback still silently compensates for it, fail the gate and open the graph-invariant corrective playbook.

### Corrective template
- `subbundles/_corrective-graph-invariant-reset`

### Detailed execution notes
- The canonical dependency shape is now much better; the remaining canonicity problem is graph legality, not legacy scalar mirrors.
- Do not implement cycle handling only in the UI. The save/publish boundary must reject it in the service layer.

