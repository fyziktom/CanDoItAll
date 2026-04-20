# Corrective renderer-boundary reset

## Status

- Blocked

## Objective

- Repair any failure where the new WebGL library stopped being universal, the runtime boundary drifted into Blazor/server round trips, or the JS/asset strategy became unstable.

## Covered Inputs

- `RQ-03`
- `RQ-04`
- `RQ-05`
- `RQ-06`
- `RQ-17`
- `RQ-18`

## Prerequisites

- Prepared bundle readiness gate passed.
- No downstream implementation may begin until this subbundle owns the active work item.

## Exact Source References

- C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/Components/Workbench/CanvasWorkbench.razor
- C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/07-runtime-entry.js
- C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/CanDoItAll.Components.CanvasLib.csproj
- C:/repositories/CanDoItAll/CanDoItAll.slnx

## Deliverables

- A focused corrective change set that restores the universal library boundary and JS-owned runtime control.
- Updated gate memo explaining what drifted and how it was corrected.
- Refreshed foundation proof after the repair.

## Dependency Impact

- This corrective must complete before any projection, sandbox, or automation work resumes.

## Validation Depth

- Critical corrective
- Build + refreshed foundation smoke proof

## Implementation Steps

1. Identify the exact place where Processes semantics or per-frame .NET behavior leaked into the library.
2. Refactor the boundary so the library returns to generic contracts and JS-owned frame-loop control.
3. Refresh the relevant tests, smoke proof, and gate memo before rerunning Gate A.


## Do Not Do

- Do not patch around a bad boundary while leaving the architectural drift intact.
- Do not continue downstream work before rerunning the failed gate.

## Acceptance Checklist

- The universal library boundary is restored.
- Per-frame behavior is back in JS and no forbidden module dependency remains.
- Gate A can be rerun on fresh proof.

## Proof Required

- Build the solution.
- Refresh the generic smoke proof and any contract-guard tests.
- Update the corrective memo and rerun Gate A.
- Validation commands to run for this subbundle:
- `dotnet build CanDoItAll.slnx -v:minimal`

## Browser Validation Logging

- Re-check the smallest possible scene to confirm the repaired runtime still boots and labels remain visible.

## Progression Gate

- Downstream work remains blocked until Gate A passes on fresh proof after this corrective.

## Suggested Agent Prompt

```text
Execute only the corrective renderer-boundary reset. Repair the universal-library and JS-runtime boundary, refresh foundation proof, rerun Gate A, and do not resume downstream work until the gate explicitly passes.
```

## Preserved Bundle Notes

### Review questions

- Was the actual architectural drift removed rather than hidden?
- Is the library universal again?
- Can Gate A now pass honestly?

### Validation commands

- `dotnet build CanDoItAll.slnx -v:minimal`

### Corrective trigger

- This subbundle is itself a corrective playbook.

### Corrective template

- Not applicable.

### Repository touchpoints (relative)

- `src/CanDoItAll.Components.CanvasLib/Components/Workbench/CanvasWorkbench.razor`
- `src/CanDoItAll.Components.CanvasLib/wwwroot/js/runtime/workbench/07-runtime-entry.js`
- `src/CanDoItAll.Components.CanvasLib/CanDoItAll.Components.CanvasLib.csproj`
- `CanDoItAll.slnx`

### Notes

- Treat this subbundle as an isolated execution slice. Do not continue into later numbered work during the same pass.
- Update `reviews/01-execution-report.md` and `reviews/02-architecture-gate-memo-log.md` as soon as this subbundle either passes, blocks, or triggers a corrective path.
