# Performance, scaling, and structural follow-up

## Status

- Prepared

## Objective

- Apply targeted performance and concentration cleanup now that correctness is closed: reduce hot repeated scans, clean dead locals, document or improve analytics aggregation, and trim low-value duplication without opening a risky rewrite.

## Covered Inputs

- See `02-open-findings.md`, `requirements/01-normalized-requirements.md`, and `traceability/01-finding-to-subbundle-map.md` for the owning findings and requirements.

## Prerequisites

- Follow the dependencies recorded in `codex/TASKS.json` and `plan/01-phase-plan.md`.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Persistence.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeProgressionPlanner.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.RuntimeReadQuery.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasRecompositionService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessOutbox.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Support.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessTemplateLibraryService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Presenters.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs

## Dependency Impact

- Downstream work remains blocked until this subbundle's progression gate is satisfied from fresh proof.

## Validation Depth

- `Targeted follow-up`

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
- If performance cleanup begins to reopen correctness or widens into a rewrite, stop and create a corrective subbundle instead of continuing.

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
Implement only subbundle 11-performance-scaling-and-structural-follow-up. Apply targeted performance and concentration cleanup now that correctness is closed: reduce hot repeated scans, clean dead locals, document or improve analytics aggregation, and trim low-value duplication without opening a risky rewrite. Respect the prerequisites, stop rules, and proof contract, update the live execution report from fresh evidence, and do not continue downstream until the progression gate is explicitly satisfied.
```

## Preserved Bundle Notes

### Purpose
Apply targeted performance and concentration cleanup now that correctness is closed: reduce hot repeated scans, clean dead locals, document or improve analytics aggregation, and trim low-value duplication without opening a risky rewrite.

### Required deliverables
- Precomputed lookups or other targeted complexity reductions in known hot loops such as differential save and progression planning.
- Cleanup of dead locals or obviously unused intermediate collections such as the current unused `orderedDependencies` local.
- A short performance note for analytics/read-side aggregation that either improves the query shape or documents the remaining data-size assumptions honestly.
- Small helper deduplication where duplication is still low-risk and obvious, such as repeated route builders.
- No blanket rewrite of the Process module or large renaming churn.

### Repository touchpoints
- `src/CanDoItAll.Modules.Processes/ProcessesService.Persistence.cs`
- `src/CanDoItAll.Modules.Processes/ProcessRuntimeProgressionPlanner.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.RuntimeReadQuery.cs`
- `src/CanDoItAll.Modules.Processes/ProcessCanvasRecompositionService.cs`
- `src/CanDoItAll.Modules.Processes/ProcessOutbox.cs`
- `src/CanDoItAll.Modules.Processes/ProcessesService.Support.cs`
- `src/CanDoItAll.Modules.Processes/ProcessTemplateLibraryService.cs`
- `src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Presenters.cs`
- `tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`
- `tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs`

### Validation commands
- `dotnet build CanDoItAll.slnx -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessesServiceIntegrationTests|FullyQualifiedName~ProcessSchemaIntegrationTests|FullyQualifiedName~ProcessOutboxIntegrationTests" -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProcessWorkspaceTests|FullyQualifiedName~ProcessCanvasSurfaceFactoryTests|FullyQualifiedName~ProcessStepEditorFormTests" -v:minimal`

### Review questions
1. Were the most obvious repeated-scan hotspots reduced without destabilizing the already-hardened invariants?
2. Is the performance note honest about remaining scale assumptions instead of claiming unlimited scalability?
3. Did structural cleanup avoid reopening earlier invariants or re-concentrating logic into `ProcessesService`?

### Corrective trigger
If performance cleanup begins to reopen correctness or widens into a rewrite, stop and create a corrective subbundle instead of continuing.

### Corrective template
- `subbundles/_corrective-template`

### Detailed execution notes
- This phase is intentionally medium priority. Correctness comes first.
- Long files may remain, but trim only where the seam is now obvious and low risk.

